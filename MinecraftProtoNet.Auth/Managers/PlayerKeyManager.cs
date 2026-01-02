using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MinecraftProtoNet.Auth.Dtos;
using Serilog;

namespace MinecraftProtoNet.Auth.Managers;

public sealed class PlayerKeyManager : IDisposable
{
    #region Configuration

    private readonly HttpClient _httpClient = new();
    private readonly string _minecraftAccessToken;
    private readonly Guid _playerUuid;
    private readonly string _privateKeyPath;
    private RSA? _playerPrivateKey;
    private bool _disposed;

    private PublicKeyInfo? _currentPublicKeyInfo;
    private ChatSessionInfo? _currentChatSessionInfo;

    private const string PrivateKeyFileName = "profile_private_key.pem";
    private const string PublicKeyInfoFileName = "profile_public_key_info.json";
    private const int RsaKeySize = 2048;
    private static readonly TimeSpan KeyValidityDuration = TimeSpan.FromHours(48);

    public PlayerKeyManager(AuthResult authResult, string configDirectory)
    {
        _minecraftAccessToken = authResult.MinecraftAccessToken ?? throw new ArgumentNullException(nameof(authResult.MinecraftAccessToken));
        _playerUuid = authResult.Uuid;

        Directory.CreateDirectory(configDirectory);
        _privateKeyPath = Path.Combine(configDirectory, PrivateKeyFileName);
        var publicKeyInfoPath = Path.Combine(configDirectory, PublicKeyInfoFileName);

        LoadPublicKeyInfoFromFile(publicKeyInfoPath);
    }

    #endregion

    #region Manager

    public RSA? PlayerPrivateKey => _playerPrivateKey;

    public async Task<(RSA? PrivateKey, ChatSessionInfo? SessionInfo)> EnsureKeysAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Check if we already have valid keys loaded in memory
        if (_playerPrivateKey != null && IsCurrentPublicKeyValid() && _currentChatSessionInfo != null)
        {
            // Verify the keys match before using
            if (DoKeysMatch(_playerPrivateKey, _currentChatSessionInfo.PublicKeyDer))
            {
                Log.Information("[KeyManager INFO] Using existing valid player keys and chat session info");
                return (_playerPrivateKey, _currentChatSessionInfo);
            }
            Log.Warning("[KeyManager WARN] In-memory keys don't match, will reload/regenerate");
        }

        Log.Information("[KeyManager INFO] Existing keys/session info not loaded or expired. Checking local storage...");

        _playerPrivateKey = LoadPrivateKeyFromFile();
        LoadChatSessionInfoFromFile(GetChatSessionInfoPath());

        // Verify loaded keys match each other
        if (_playerPrivateKey != null && IsCurrentPublicKeyValid() && _currentChatSessionInfo != null)
        {
            if (DoKeysMatch(_playerPrivateKey, _currentChatSessionInfo.PublicKeyDer))
            {
                Log.Information("[KeyManager INFO] Loaded valid player keys and session info from storage (keys verified to match)");
                return (_playerPrivateKey, _currentChatSessionInfo);
            }
            Log.Warning("[KeyManager WARN] Cached private key does NOT match public key in chat session info! Forcing regeneration...");
        }

        Log.Information("[KeyManager INFO] Local keys/session info missing, expired, or inconsistent. Generating/refreshing...");
        _playerPrivateKey?.Dispose();
        _playerPrivateKey = null;
        _currentPublicKeyInfo = null;
        _currentChatSessionInfo = null;
        
        // Delete stale cached files to prevent future mismatches
        DeleteCachedKeyFiles();

        // Generate a temporary key for the upload request signature
        using var tempGeneratedKey = RSA.Create(RsaKeySize);
        Log.Information("[KeyManager INFO] Generated temporary {RsaKeySize}-bit RSA key for upload request", RsaKeySize);

        // Upload to Mojang and get THEIR key pair (may differ from our generated one!)
        var (mojangPrivateKey, sessionInfo) = await UploadPublicKeyAndGetSessionInfoAsync(tempGeneratedKey);
        if (mojangPrivateKey == null || sessionInfo == null)
        {
            Log.Error("[KeyManager ERROR] Failed to upload public key or retrieve session info from Mojang");
            return (null, null);
        }

        // Use Mojang's private key, not our generated one!
        _playerPrivateKey = mojangPrivateKey;
        _currentChatSessionInfo = sessionInfo;

        if (!SavePrivateKeyToFile(_playerPrivateKey))
        {
            Log.Warning("[KeyManager WARN] Failed to save new private key locally");
        }

        SavePublicKeyInfoToFile(GetPublicKeyInfoPath());
        SaveChatSessionInfoToFile(GetChatSessionInfoPath());

        Log.Information("[KeyManager INFO] Mojang keys imported, uploaded, and session info retrieved/saved");
        return (_playerPrivateKey, _currentChatSessionInfo);
    }

    /// <summary>
    /// Verifies that a private key matches a public key (in DER format).
    /// </summary>
    private static bool DoKeysMatch(RSA privateKey, byte[] publicKeyDer)
    {
        try
        {
            var derivedPublicKey = privateKey.ExportSubjectPublicKeyInfo();
            return derivedPublicKey.AsSpan().SequenceEqual(publicKeyDer);
        }
        catch (Exception ex)
        {
            Log.Warning("[KeyManager WARN] Failed to verify key pair match: {Message}", ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// Deletes all cached key files to force fresh regeneration.
    /// </summary>
    private void DeleteCachedKeyFiles()
    {
        try
        {
            var filesToDelete = new[]
            {
                _privateKeyPath,
                GetPublicKeyInfoPath(),
                GetChatSessionInfoPath()
            };
            
            foreach (var file in filesToDelete)
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    Log.Information("[KeyManager INFO] Deleted stale cache file: {File}", Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[KeyManager WARN] Error deleting cached key files: {Message}", ex.Message);
        }
    }

    #endregion

    #region File Operations

    private string GetPublicKeyInfoPath() => Path.Combine(Path.GetDirectoryName(_privateKeyPath) ?? ".", PublicKeyInfoFileName);
    private const string ChatSessionInfoFileName = "chat_session_info.json";
    private string GetChatSessionInfoPath() => Path.Combine(Path.GetDirectoryName(_privateKeyPath) ?? ".", ChatSessionInfoFileName);

    private void LoadChatSessionInfoFromFile(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<ChatSessionInfoDto>(json);
            if (dto != null)
            {
                _currentChatSessionInfo = new ChatSessionInfo
                {
                    PublicKeyDer = Convert.FromBase64String(dto.PublicKeyDerB64),
                    ExpiresAtEpochMs = dto.ExpiresAtEpochMs,
                    MojangSignature = Convert.FromBase64String(dto.MojangSignatureB64)
                };
            }
            else
            {
                _currentChatSessionInfo = null;
            }
        }
        catch (Exception ex)
        {
            Log.Warning("[KeyManager WARN] Failed to load chat session info from '{Path}': {ExMessage}", path, ex.Message);
            _currentChatSessionInfo = null;
        }
    }

    private record ChatSessionInfoDto
    {
        public required string PublicKeyDerB64 { get; init; }
        public long ExpiresAtEpochMs { get; init; }
        public required string MojangSignatureB64 { get; init; }
    }

    private void SaveChatSessionInfoToFile(string path)
    {
        if (_currentChatSessionInfo == null) return;
        try
        {
            // Need a serializable DTO for ChatSessionInfo
            var dto = new ChatSessionInfoDto
            {
                PublicKeyDerB64 = Convert.ToBase64String(_currentChatSessionInfo.PublicKeyDer),
                ExpiresAtEpochMs = _currentChatSessionInfo.ExpiresAtEpochMs,
                MojangSignatureB64 = Convert.ToBase64String(_currentChatSessionInfo.MojangSignature)
            };
            var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Log.Warning("[KeyManager WARN] Failed to save chat session info to '{Path}': {ExMessage}", path, ex.Message);
        }
    }

    private RSA? LoadPrivateKeyFromFile()
    {
        if (!File.Exists(_privateKeyPath))
        {
            return null;
        }

        try
        {
            var pem = File.ReadAllText(_privateKeyPath);
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem.ToCharArray());
            return rsa;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[KeyManager ERROR] Failed to load private key from \'{PrivateKeyPath}\'", _privateKeyPath);
            return null;
        }
    }

    private bool SavePrivateKeyToFile(RSA privateKey)
    {
        try
        {
            var pem = privateKey.ExportPkcs8PrivateKeyPem();
            File.WriteAllText(_privateKeyPath, pem);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[KeyManager ERROR] Failed to save private key to \'{PrivateKeyPath}\'", _privateKeyPath);
            return false;
        }
    }

    private void LoadPublicKeyInfoFromFile(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            var json = File.ReadAllText(path);
            _currentPublicKeyInfo = JsonSerializer.Deserialize<PublicKeyInfo>(json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[KeyManager WARN] Failed to load public key info from \'{Path}\': Will assume keys need refresh", path);
            _currentPublicKeyInfo = null;
        }
    }

    private void SavePublicKeyInfoToFile(string path)
    {
        if (_currentPublicKeyInfo == null) return;
        try
        {
            var json = JsonSerializer.Serialize(_currentPublicKeyInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[KeyManager WARN] Failed to save public key info to \'{Path}\'", path);
        }
    }

    private bool IsCurrentPublicKeyValid()
    {
        return _currentPublicKeyInfo != null && _currentPublicKeyInfo.ExpiresAt > DateTimeOffset.UtcNow;
    }

    #endregion

    #region Mojang API

    private async Task<(RSA? PrivateKey, ChatSessionInfo? SessionInfo)> UploadPublicKeyAndGetSessionInfoAsync(RSA generatedPrivateKey)
    {
        Log.Information("[KeyManager INFO] Attempting to upload public key and retrieve session info...");
        try
        {
            var expiresAt = DateTimeOffset.UtcNow.Add(KeyValidityDuration);
            var expiresAtMillis = expiresAt.ToUnixTimeMilliseconds();
            var publicKeyDer = generatedPrivateKey.ExportSubjectPublicKeyInfo();
            var publicKeyPemToSend = generatedPrivateKey.ExportSubjectPublicKeyInfoPem();

            var uuidString = _playerUuid.ToString("N");
            byte[] dataToSign;
            using (var ms = new MemoryStream())
            {
                ms.Write(Encoding.UTF8.GetBytes(uuidString), 0, uuidString.Length);
                ms.WriteByte((byte)'.');
                ms.Write(Encoding.UTF8.GetBytes(expiresAtMillis.ToString()), 0, expiresAtMillis.ToString().Length);
                ms.WriteByte((byte)'.');
                ms.Write(publicKeyDer, 0, publicKeyDer.Length);
                dataToSign = ms.ToArray();
            }

            var clientSignatureBytes = generatedPrivateKey.SignData(dataToSign, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            var payload = new UploadCertificateRequest
            {
                PublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(publicKeyPemToSend)),
                PublicKeySignatureV2 = Convert.ToBase64String(clientSignatureBytes),
                ExpiresAt = expiresAt.ToString("o")
            };
            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _minecraftAccessToken);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.PostAsync("https://api.minecraftservices.com/player/certificates", content);

            if (response.IsSuccessStatusCode)
            {
                Log.Information("[KeyManager INFO] Public key uploaded successfully. Parsing response...");
                var responseBody = await response.Content.ReadAsStringAsync();
                MojangCertificateResponse? mojangResponse;

                try
                {
                    mojangResponse = JsonSerializer.Deserialize<MojangCertificateResponse>(responseBody);
                }
                catch (JsonException jsonEx)
                {
                    Log.Error(jsonEx, "[KeyManager ERROR] Failed to deserialize Mojang response JSON");
                    Log.Debug("[KeyManager DEBUG] Raw Response Body: {RawBody}", responseBody);
                    return (null, null);
                }

                if (mojangResponse?.KeyPair?.PublicKey == null || mojangResponse.PublicKeySignatureV2 == null)
                {
                    Log.Error("[KeyManager ERROR] Mojang response missing required fields (KeyPair.PublicKey or SignatureV2)");
                    Log.Debug("[KeyManager DEBUG] Raw Response Body: {RawBody}", responseBody);
                    return (null, null);
                }

                // CRITICAL: Import Mojang's returned private key, not our generated one!
                // Mojang returns their own key pair which may differ from what we uploaded.
                RSA? mojangPrivateKey = null;
                if (!string.IsNullOrEmpty(mojangResponse.KeyPair.PrivateKey))
                {
                    var privatePem = mojangResponse.KeyPair.PrivateKey;
                    
                    // Log the PEM header for debugging
                    var firstLine = privatePem.Split('\n').FirstOrDefault()?.Trim() ?? "";
                    Log.Debug("[KeyManager DEBUG] Private key PEM starts with: {Header}", firstLine);
                    
                    try
                    {
                        mojangPrivateKey = RSA.Create();
                        
                        // Try ImportFromPem first (handles both PKCS#1 and PKCS#8 PEM formats)
                        try
                        {
                            mojangPrivateKey.ImportFromPem(privatePem.ToCharArray());
                            Log.Information("[KeyManager INFO] Successfully imported Mojang's private key via ImportFromPem");
                        }
                        catch (Exception pemEx)
                        {
                            Log.Debug("[KeyManager DEBUG] ImportFromPem failed: {Message}, trying alternative methods...", pemEx.Message);
                            
                            // Extract base64 content and try manual import
                            var lines = privatePem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                            var base64Content = string.Join("", lines
                                .Where(l => !l.StartsWith("-----"))
                                .Select(l => l.Trim()));
                            var keyBytes = Convert.FromBase64String(base64Content);
                            
                            // Try PKCS#8 format first (-----BEGIN PRIVATE KEY-----)
                            try
                            {
                                mojangPrivateKey.ImportPkcs8PrivateKey(keyBytes, out _);
                                Log.Information("[KeyManager INFO] Successfully imported Mojang's private key via ImportPkcs8PrivateKey");
                            }
                            catch
                            {
                                // Try RSA private key format (-----BEGIN RSA PRIVATE KEY-----)
                                mojangPrivateKey.ImportRSAPrivateKey(keyBytes, out _);
                                Log.Information("[KeyManager INFO] Successfully imported Mojang's private key via ImportRSAPrivateKey");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "[KeyManager ERROR] Failed to import Mojang's private key PEM");
                        Log.Debug("[KeyManager DEBUG] Private key PEM content:\n{Pem}", privatePem);
                        mojangPrivateKey?.Dispose();
                        return (null, null);
                    }
                }
                else
                {
                    Log.Warning("[KeyManager WARN] Mojang did not return a private key, using our generated key");
                    mojangPrivateKey = generatedPrivateKey;
                }

                var receivedPublicKeyPem = mojangResponse.KeyPair.PublicKey;

                _currentPublicKeyInfo = new PublicKeyInfo
                {
                    PublicKeyPem = receivedPublicKeyPem,
                    ExpiresAt = mojangResponse.ExpiresAt
                };

                var mojangSignature = Convert.FromBase64String(mojangResponse.PublicKeySignatureV2);
                var receivedExpiresAtMs = mojangResponse.ExpiresAt.ToUnixTimeMilliseconds();

                byte[] receivedPublicKeyDer;
                try
                {
                    var lines = receivedPublicKeyPem.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var base64Payload = string.Join("", lines.Skip(1).Take(lines.Length - 2)).Replace("\r", "");

                    receivedPublicKeyDer = Convert.FromBase64String(base64Payload);

                    Log.Information("[KeyManager DEBUG] Successfully extracted {Length} DER bytes from received public key PEM",
                        receivedPublicKeyDer.Length);
                }
                catch (FormatException formatEx)
                {
                    Log.Error(formatEx, "[KeyManager ERROR] Failed to decode Base64 payload from received PEM.");
                    mojangPrivateKey.Dispose();
                    return (null, null);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[KeyManager ERROR] Failed to parse the public key PEM received from Mojang. PEM:\n{Pem}",
                        mojangResponse.KeyPair.PublicKey);
                    mojangPrivateKey.Dispose();
                    return (null, null);
                }

                // Verify the returned private key matches the returned public key
                var derivedPublicKey = mojangPrivateKey.ExportSubjectPublicKeyInfo();
                if (!derivedPublicKey.AsSpan().SequenceEqual(receivedPublicKeyDer))
                {
                    Log.Error("[KeyManager ERROR] Mojang's returned private key does NOT match the returned public key!");
                    mojangPrivateKey.Dispose();
                    return (null, null);
                }
                Log.Information("[KeyManager INFO] Verified: Mojang's private key matches their public key");

                var sessionInfo = new ChatSessionInfo
                {
                    PublicKeyDer = receivedPublicKeyDer,
                    ExpiresAtEpochMs = receivedExpiresAtMs,
                    MojangSignature = mojangSignature,
                    ChatContext = new ChatContext()
                };

                return (mojangPrivateKey, sessionInfo);
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            Log.Error("[KeyManager ERROR] Failed to upload public key. Status: {StatusCode}. Body: {ErrorBody}", response.StatusCode,
                errorBody);
            _currentPublicKeyInfo = null;
            return (null, null);
        }
        catch (FormatException formatEx)
        {
            Log.Error(formatEx, "[KeyManager ERROR] Failed to decode Base64 data from Mojang's response (likely MojangSignatureV2)");
            return (null, null);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[KeyManager ERROR] Unexpected error during public key upload/processing");
            _currentPublicKeyInfo = null;
            return (null, null);
        }
    }

    #endregion

    #region Helper Records

    private record UploadCertificateRequest
    {
        [JsonPropertyName("publicKey")] public required string PublicKey { get; init; }

        [JsonPropertyName("publicKeySignatureV2")]
        public required string PublicKeySignatureV2 { get; init; }

        [JsonPropertyName("expiresAt")] public required string ExpiresAt { get; init; }
    }

    private record PublicKeyInfo
    {
        public required string PublicKeyPem { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }

    private record MojangCertificateResponse
    {
        [JsonPropertyName("keyPair")] public KeyPairData? KeyPair { get; init; }

        [JsonPropertyName("publicKeySignatureV2")]
        public string? PublicKeySignatureV2 { get; init; }

        [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; init; }
        [JsonPropertyName("refreshedAfter")] public DateTimeOffset RefreshedAfter { get; init; }
    }

    private record KeyPairData
    {
        [JsonPropertyName("publicKey")] public string? PublicKey { get; init; }
        [JsonPropertyName("privateKey")] public string? PrivateKey { get; init; }
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _httpClient?.Dispose();
            _playerPrivateKey?.Dispose();
        }

        _playerPrivateKey = null;
        _disposed = true;
    }

    ~PlayerKeyManager()
    {
        Dispose(false);
    }

    #endregion
}
