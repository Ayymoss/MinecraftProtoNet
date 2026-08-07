using System.Net;
using System.Text;
using System.Text.Json;

namespace MinecraftProtoNet.ClaudeHarness;

/// <summary>
/// What the trading session is doing right now, as a page a human can leave open.
///
/// Served by the harness itself rather than written as a file, because the interesting question during a run
/// is always "what is it doing NOW" — and a page that polls answers that without anyone tailing a log.
/// </summary>
public sealed class StatusServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly object _gate = new();
    private readonly List<string> _events = [];
    private CancellationTokenSource? _cts;

    public int Port { get; }

    public StatusServer(int port)
    {
        Port = port;
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    // Snapshot fields, written by the trader each cycle and read by whoever loads the page.
    public string State { get; set; } = "starting";
    public string Hub { get; set; } = "unknown";
    public string Server { get; set; } = "";
    public string Position { get; set; } = "";
    public bool Connected { get; set; }
    public bool Intercepted { get; set; }
    public double Capital { get; set; }
    public double Committed { get; set; }
    public double RealisedProfit { get; set; }
    public int ClosedCount { get; set; }
    public int ProfitableCount { get; set; }
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public List<object> OpenPositions { get; set; } = [];
    public List<object> ClosedPositions { get; set; } = [];

    /// <summary>Adds a line to the rolling activity feed shown on the page.</summary>
    public void Note(string message)
    {
        lock (_gate)
        {
            _events.Add($"{DateTime.Now:HH:mm:ss}  {message}");
            if (_events.Count > 400) _events.RemoveRange(0, _events.Count - 400);
        }
    }

    public void Start()
    {
        _listener.Start();
        _cts = new CancellationTokenSource();
        _ = ServeAsync(_cts.Token);
    }

    private async Task ServeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch
            {
                return; // listener stopped
            }

            try
            {
                var path = context.Request.Url?.AbsolutePath ?? "/";
                var (body, contentType) = path == "/status.json"
                    ? (BuildJson(), "application/json")
                    : (Page, "text/html; charset=utf-8");

                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.ContentType = contentType;
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, ct);
            }
            catch
            {
                // A dropped request must never disturb trading.
            }
            finally
            {
                try { context.Response.Close(); } catch { /* best-effort */ }
            }
        }
    }

    private string BuildJson()
    {
        lock (_gate)
        {
            return JsonSerializer.Serialize(new
            {
                state = State,
                hub = Hub,
                server = Server,
                position = Position,
                connected = Connected,
                intercepted = Intercepted,
                capital = Capital,
                committed = Committed,
                realisedProfit = RealisedProfit,
                closedCount = ClosedCount,
                profitableCount = ProfitableCount,
                runningMinutes = (DateTime.UtcNow - StartedUtc).TotalMinutes,
                open = OpenPositions,
                closed = ClosedPositions,
                events = Enumerable.Reverse(_events).Take(120).ToList()
            });
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _listener.Stop(); } catch { /* best-effort */ }
        _listener.Close();
    }

    private const string Page = """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<title>Bazaar bot</title>
<style>
  :root { color-scheme: dark; }
  body { background:#12141a; color:#e6e8ee; font:14px/1.5 ui-monospace,Consolas,monospace; margin:0; padding:24px; }
  h1 { font-size:18px; margin:0 0 4px; }
  .sub { color:#8b93a7; margin-bottom:20px; }
  .cards { display:flex; gap:12px; flex-wrap:wrap; margin-bottom:20px; }
  .card { background:#1b1e27; border:1px solid #272b37; border-radius:8px; padding:12px 16px; min-width:150px; }
  .card .label { color:#8b93a7; font-size:11px; text-transform:uppercase; letter-spacing:.06em; }
  .card .value { font-size:20px; margin-top:4px; }
  table { border-collapse:collapse; width:100%; margin-bottom:20px; }
  th,td { text-align:left; padding:6px 10px; border-bottom:1px solid #272b37; }
  th { color:#8b93a7; font-weight:normal; font-size:12px; text-transform:uppercase; letter-spacing:.05em; }
  .pos { color:#6ee7a8; } .neg { color:#ff8080; } .warn { color:#ffd479; }
  .feed { background:#1b1e27; border:1px solid #272b37; border-radius:8px; padding:12px 16px; max-height:340px; overflow:auto; white-space:pre-wrap; }
  .halted { background:#3a1418; border:1px solid #7a2630; padding:12px 16px; border-radius:8px; margin-bottom:20px; }
</style>
</head>
<body>
<h1>Bazaar bot</h1>
<div class="sub" id="sub">connecting…</div>
<div id="halt"></div>
<div class="cards" id="cards"></div>
<h3>Open positions</h3>
<table id="open"><thead><tr><th>Product</th><th>Leg</th><th>Qty</th><th>Price</th><th>Held</th><th>Spent</th><th>Age</th><th>Steps</th></tr></thead><tbody></tbody></table>
<h3>Closed</h3>
<table id="closed"><thead><tr><th>Product</th><th>Bought</th><th>Cost</th><th>Sold</th><th>Received</th><th>Profit</th></tr></thead><tbody></tbody></table>
<h3>Activity</h3>
<div class="feed" id="feed"></div>
<script>
const coins = n => (n ?? 0).toLocaleString(undefined,{maximumFractionDigits:1});
const cls = n => n > 0 ? 'pos' : n < 0 ? 'neg' : '';
async function tick() {
  try {
    const r = await fetch('/status.json'); const d = await r.json();
    document.getElementById('sub').textContent =
      `${d.state} · ${d.hub}${d.server ? ' ('+d.server+')' : ''} · ${d.position} · ${d.connected ? 'connected' : 'DISCONNECTED'} · running ${d.runningMinutes.toFixed(0)}m`;
    document.getElementById('halt').innerHTML = d.intercepted
      ? '<div class="halted"><b>HALTED — possible admin intercept.</b> The bot disconnected and will not reconnect until acknowledged.</div>' : '';
    document.getElementById('cards').innerHTML = [
      ['Realised P&L', `<span class="${cls(d.realisedProfit)}">${coins(d.realisedProfit)}</span>`],
      ['Closed', `${d.profitableCount}/${d.closedCount} profitable`],
      ['Open', d.open.length],
      ['Committed', `${coins(d.committed)} / ${coins(d.capital)}`],
    ].map(([l,v]) => `<div class="card"><div class="label">${l}</div><div class="value">${v}</div></div>`).join('');
    document.querySelector('#open tbody').innerHTML = d.open.map(p =>
      `<tr><td>${p.name}</td><td>${p.side}</td><td>${p.quantity}</td><td>${coins(p.price)}</td><td>${p.unitsBought}</td><td>${coins(p.spent)}</td><td>${p.ageMinutes.toFixed(1)}m</td><td>${p.steps}</td></tr>`).join('')
      || '<tr><td colspan="8">none</td></tr>';
    document.querySelector('#closed tbody').innerHTML = d.closed.map(p =>
      `<tr><td>${p.name}</td><td>${p.unitsBought}</td><td>${coins(p.spent)}</td><td>${p.unitsSold}</td><td>${coins(p.received)}</td><td class="${cls(p.profit)}">${coins(p.profit)}</td></tr>`).join('')
      || '<tr><td colspan="6">none yet</td></tr>';
    document.getElementById('feed').textContent = d.events.join('\n');
  } catch (e) {
    document.getElementById('sub').textContent = 'status unavailable — the run may have ended';
  }
}
tick(); setInterval(tick, 3000);
</script>
</body>
</html>
""";
}
