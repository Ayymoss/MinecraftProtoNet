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

    /// <summary>
    /// Builds the current snapshot, called once per HTTP request.
    ///
    /// The trader used to push a snapshot at the top of each cycle, which meant the page showed the state
    /// from BEFORE that cycle's work — during a long cycle (opening four positions takes minutes) the tables
    /// sat empty while the activity feed scrolled, and a cycle that failed early published nothing at all.
    /// Pulling on request cannot go stale.
    /// </summary>
    public Func<object>? SnapshotProvider { get; set; }

    // Fallback fields, used until a provider is attached.
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

    /// <summary>
    /// When the current UNINTERRUPTED stay on the server began — reset on every ejection/disconnect.
    ///
    /// Kept separate from StartedUtc because the two answer different questions and conflating them hid the
    /// one that matters. "running 60m" was true of the process while the bot had been thrown off and
    /// reconnected several times inside that hour, which reads as an hour of survival. Time-since-last-
    /// ejection IS the metric the ejection work is judged on, so it gets its own field.
    /// </summary>
    public DateTime SessionStartedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Ejections/disconnects observed this process, and the longest clean stay so far (minutes).</summary>
    public int EjectionCount { get; set; }
    public double BestSessionMinutes { get; set; }

    /// <summary>
    /// Call on every ejection/disconnect: banks the stay that just ended and restarts the clean-stay clock.
    /// </summary>
    public void NoteEjection()
    {
        var lasted = (DateTime.UtcNow - SessionStartedUtc).TotalMinutes;
        if (lasted > BestSessionMinutes) BestSessionMinutes = lasted;
        EjectionCount++;
        SessionStartedUtc = DateTime.UtcNow;
        Note($"ejected after {lasted:F1}m clean (ejection #{EjectionCount}, best {BestSessionMinutes:F1}m)");
    }

    /// <summary>Call when a fresh connection is established, so the clean-stay clock starts at spawn.</summary>
    public void NoteConnected() => SessionStartedUtc = DateTime.UtcNow;
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
        // Retry the bind rather than giving up on the first failure.
        //
        // Windows keeps an HttpListener's URL registration for a short while after the owning process dies,
        // so a run that is stopped and immediately restarted — which is exactly what the ejection-bisect
        // harness does between modes — hits "conflicts with an existing registration on the machine" and the
        // page never comes back. The bot itself carries on fine, which makes it worse: the only screen a human
        // can watch goes dark while everything looks healthy from the inside.
        const int attempts = 12;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                _listener.Start();
                break;
            }
            catch (HttpListenerException) when (attempt < attempts)
            {
                Thread.Sleep(1000);
            }
        }

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
        if (SnapshotProvider is { } provider)
        {
            try
            {
                var snapshot = provider();
                lock (_gate)
                {
                    return JsonSerializer.Serialize(new
                    {
                        snapshot,
                        events = Enumerable.Reverse(_events).Take(120).ToList()
                    });
                }
            }
            catch (Exception ex)
            {
                // Never let a reporting fault reach the trading loop, and never serve a half-built page.
                lock (_gate)
                {
                    return JsonSerializer.Serialize(new
                    {
                        state = $"status unavailable: {ex.Message}",
                        events = Enumerable.Reverse(_events).Take(120).ToList()
                    });
                }
            }
        }

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
                cleanMinutes = (DateTime.UtcNow - SessionStartedUtc).TotalMinutes,
                ejectionCount = EjectionCount,
                bestSessionMinutes = BestSessionMinutes,
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
    /// <summary>
    /// The monitor page.
    ///
    /// Laid out as a dashboard that fits one screen rather than a column of tables. The stacked version grew
    /// past the bottom of the window as soon as a few flips closed, which is precisely when the page stops
    /// being useful — the numbers worth watching scrolled out of sight. Every panel now scrolls inside itself,
    /// so the header and the summary figures stay put however long the history gets.
    /// </summary>
    private const string Page = """
<!doctype html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Bazaar bot</title>
<style>
  :root {
    color-scheme: dark;
    --bg:#0f1116; --panel:#171a22; --line:#262b36; --text:#e6e8ee; --dim:#8b93a7;
    --pos:#6ee7a8; --neg:#ff8080; --warn:#ffd479; --accent:#5b8cff;
  }
  * { box-sizing: border-box; }
  html, body { height:100%; }
  body {
    margin:0; background:var(--bg); color:var(--text);
    font:13px/1.45 ui-monospace,SFMono-Regular,Consolas,monospace;
    display:flex; flex-direction:column; overflow:hidden;
  }

  header { padding:12px 16px 10px; border-bottom:1px solid var(--line); flex:none; }
  .titlerow { display:flex; align-items:baseline; gap:12px; flex-wrap:wrap; }
  h1 { font-size:15px; margin:0; letter-spacing:.02em; }
  .sub { color:var(--dim); font-size:12px; }
  .dot { display:inline-block; width:7px; height:7px; border-radius:50%; background:var(--pos); margin-right:5px; }
  .dot.off { background:var(--neg); }

  .cards { display:flex; gap:8px; margin-top:10px; flex-wrap:wrap; }
  .card { background:var(--panel); border:1px solid var(--line); border-radius:7px; padding:7px 12px; min-width:120px; flex:1 1 120px; }
  .card .label { color:var(--dim); font-size:10px; text-transform:uppercase; letter-spacing:.07em; }
  .card .value { font-size:16px; margin-top:2px; white-space:nowrap; }

  /* Two columns of two panels. The book is the widest thing here, so it gets the larger column. */
  .grid {
    flex:1; min-height:0; display:grid; gap:10px; padding:10px 16px 14px;
    grid-template-columns: 1.35fr 1fr;
    grid-template-rows: minmax(0,1.15fr) minmax(0,1fr);
  }
  .panel { background:var(--panel); border:1px solid var(--line); border-radius:8px; display:flex; flex-direction:column; min-height:0; }
  .panel > h2 {
    font-size:11px; text-transform:uppercase; letter-spacing:.07em; color:var(--dim);
    margin:0; padding:8px 12px; border-bottom:1px solid var(--line); flex:none;
    display:flex; justify-content:space-between; gap:8px;
  }
  .panel > h2 .hint { text-transform:none; letter-spacing:0; opacity:.75; font-size:10px; }
  .panel .body { overflow:auto; flex:1; min-height:0; }

  table { border-collapse:collapse; width:100%; }
  th, td { text-align:left; padding:5px 12px; border-bottom:1px solid var(--line); white-space:nowrap; }
  /* Sticky headers keep the columns readable once a panel scrolls. */
  th { position:sticky; top:0; background:var(--panel); color:var(--dim); font-weight:normal; font-size:10px;
       text-transform:uppercase; letter-spacing:.05em; z-index:1; }
  tbody tr:last-child td { border-bottom:none; }
  td.num, th.num { text-align:right; }
  .pos { color:var(--pos); } .neg { color:var(--neg); } .warn { color:var(--warn); }
  /* The rate sits beside the total, smaller — it qualifies the figure rather than competing with it. */
  .rate { font-size:11px; color:var(--dim); margin-left:4px; }
  .muted { color:var(--dim); }

  /* Relative bar behind the preference score, so ranking reads without comparing digits. */
  .barcell { position:relative; }
  .bar { position:absolute; left:0; top:0; bottom:0; background:var(--accent); opacity:.16; }
  .barcell span { position:relative; }

  .feed { padding:8px 12px; white-space:pre-wrap; font-size:12px; line-height:1.5; color:#c7cbd6; }
  .halted { background:#3a1418; border:1px solid #7a2630; padding:9px 14px; border-radius:7px; margin-top:10px; }

  /* Narrow screens: one column, and let the page scroll rather than squeezing panels to nothing. */
  @media (max-width: 1080px) {
    body { overflow:auto; }
    .grid { grid-template-columns:1fr; grid-template-rows:none; grid-auto-rows:minmax(220px,auto); }
  }
</style>
</head>
<body>
<header>
  <div class="titlerow">
    <h1>Bazaar bot</h1>
    <div class="sub" id="sub">connecting…</div>
  </div>
  <div id="halt"></div>
  <div class="cards" id="cards"></div>
</header>

<div class="grid">
  <section class="panel" style="grid-row:span 2">
    <h2><span>Open positions</span><span class="hint" id="opencount"></span></h2>
    <div class="body">
      <table id="open"><thead><tr>
        <th>Product</th><th>Leg</th><th class="num">Qty</th><th class="num">Price</th>
        <th class="num">Held</th><th class="num">Spent</th><th class="num">Age</th><th class="num">Steps</th>
      </tr></thead><tbody></tbody></table>
    </div>
  </section>

  <section class="panel">
    <h2><span>Preferences</span><span class="hint">decayed to now · 6h half-life</span></h2>
    <div class="body">
      <table id="prefs"><thead><tr>
        <th>Product</th><th class="num">Coins/h</th><th class="num">Trades</th><th class="num">Total</th><th>Status</th>
      </tr></thead><tbody></tbody></table>
    </div>
  </section>

  <section class="panel">
    <h2><span>Activity</span></h2>
    <div class="body"><div class="feed" id="feed"></div></div>
  </section>
</div>

<div class="grid" style="grid-template-columns:1.35fr 1fr; grid-template-rows:minmax(0,360px); padding-top:0;">
  <section class="panel">
    <h2><span>Closed flips</span><span class="hint" id="closedsum"></span></h2>
    <div class="body">
      <table id="closed"><thead><tr>
        <th>Product</th><th class="num">Bought</th><th class="num">Cost</th>
        <th class="num">Sold</th><th class="num">Received</th><th class="num">Profit</th><th class="num">Closed</th>
      </tr></thead><tbody></tbody></table>
    </div>
  </section>
  <section class="panel">
    <h2><span>Realised P&amp;L / 30m</span><span class="hint" id="pnlsum"></span></h2>
    <div class="body" style="padding:10px 12px;">
      <div id="spark"></div>
    </div>
  </section>
</div>

<script>
const coins = n => (n ?? 0).toLocaleString(undefined,{maximumFractionDigits:1});
const cls = n => n > 0 ? 'pos' : n < 0 ? 'neg' : '';

// Realised P&L per 30-minute bucket over 24h, as an inline SVG bar chart.
//
// Bars rather than a cumulative line: the question is "is it still earning, and how steadily", and a
// cumulative curve hides a bot that stopped trading two hours ago behind a flat-but-high line. Empty buckets
// are drawn as a baseline tick so a gap in trading is visible as a gap, not as missing data.
function drawSpark(series) {
  const host = document.getElementById('spark');
  if (!host) return;
  if (!series.length) { host.innerHTML = '<div class="muted">no closed flips in the last 24h</div>'; return; }

  const W = 100, H = 38, n = series.length, bw = W / n;
  const peak = Math.max(1, ...series.map(b => Math.abs(b.profit)));
  const mid = H / 2;

  const bars = series.map((b, i) => {
    const h = Math.abs(b.profit) / peak * (mid - 1);
    const x = (i * bw).toFixed(3), w = Math.max(0.35, bw * 0.8).toFixed(3);
    if (!b.trades) return `<rect x="${x}" y="${(mid - 0.15).toFixed(2)}" width="${w}" height="0.3" fill="#3a3f4b"/>`;
    const y = b.profit >= 0 ? mid - h : mid;
    return `<rect x="${x}" y="${y.toFixed(2)}" width="${w}" height="${Math.max(0.4, h).toFixed(2)}"`
      + ` fill="${b.profit >= 0 ? '#6ee7a8' : '#ff8080'}"><title>${new Date(b.at).toLocaleTimeString()} — `
      + `${Math.round(b.profit).toLocaleString()} over ${b.trades} trade(s)</title></rect>`;
  }).join('');

  const total = series.reduce((a, b) => a + b.profit, 0);
  const traded = series.filter(b => b.trades).length;

  host.innerHTML =
    `<svg viewBox="0 0 ${W} ${H}" preserveAspectRatio="none" style="width:100%;height:150px;display:block">`
    + `<line x1="0" y1="${mid}" x2="${W}" y2="${mid}" stroke="#3a3f4b" stroke-width="0.2"/>${bars}</svg>`
    + `<div class="muted" style="display:flex;justify-content:space-between;margin-top:6px;font-size:11px">`
    + `<span>24h ago</span><span>peak bucket ${Math.round(peak).toLocaleString()}</span><span>now</span></div>`
    + `<div style="margin-top:8px;font-size:12px">24h realised `
    + `<b class="${total >= 0 ? 'pos' : 'neg'}">${Math.round(total).toLocaleString()}</b>`
    + ` <span class="muted">across ${traded} active half-hour(s) of ${n}</span></div>`;
}

async function tick() {
  try {
    const r = await fetch('/status.json'); const raw = await r.json();
    const d = raw.snapshot ? Object.assign({}, raw.snapshot, {events: raw.events}) : raw;
    d.open = d.open || []; d.closed = d.closed || []; d.events = d.events || []; d.preferences = d.preferences || [];

    document.getElementById('sub').innerHTML =
      `<span class="dot ${d.connected ? '' : 'off'}"></span>${d.state} · ${d.hub}${d.server ? ' ('+d.server+')' : ''}`
      + ` · ${d.position} · running ${d.runningMinutes.toFixed(0)}m`
      + ` · <b>clean ${(d.cleanMinutes ?? 0).toFixed(0)}m</b>`
      + ` · ${d.ejectionCount ?? 0} ejection(s), best ${(d.bestSessionMinutes ?? 0).toFixed(0)}m`;

    document.getElementById('halt').innerHTML = d.intercepted
      ? '<div class="halted"><b>HALTED — possible admin intercept.</b> The bot disconnected and will not reconnect until acknowledged.</div>' : '';

    document.getElementById('cards').innerHTML = [
      ['Purse', d.purse == null ? '<span class="warn">unknown</span>' : coins(d.purse)],
      ['Realised P&L', `<span class="${cls(d.realisedProfit)}">${coins(d.realisedProfit)}</span>`
        + (d.realisedPerHour ? ` <span class="rate">${coins(d.realisedPerHour)}/h</span>` : '')],
      ['Closed', `${d.profitableCount}/${d.closedCount} profitable`],
      ['Open', d.open.length],
      ['Committed', `${coins(d.committed)} / ${coins(d.capital)}`],
    ].map(([l,v]) => `<div class="card"><div class="label">${l}</div><div class="value">${v}</div></div>`).join('');

    document.getElementById('opencount').textContent = `${d.open.length} working`;
    document.querySelector('#open tbody').innerHTML = d.open.map(p =>
      `<tr><td>${p.name}</td><td class="muted">${p.side}</td><td class="num">${p.quantity}</td>`
      + `<td class="num">${coins(p.price)}</td><td class="num">${p.unitsBought}</td><td class="num">${coins(p.spent)}</td>`
      + `<td class="num muted">${p.ageMinutes.toFixed(0)}m</td><td class="num muted">${p.steps}</td></tr>`).join('')
      || '<tr><td colspan="8" class="muted">none</td></tr>';

    // Relative, not absolute: the question this answers is "is the bot still closing trades?", and an age is
    // readable at a glance where a wall-clock time needs subtracting from now.
    const ago = m => m == null ? '<span class="muted">&mdash;</span>'
      : m < 1 ? 'just now' : m < 60 ? `${m.toFixed(0)}m ago` : `${(m/60).toFixed(1)}h ago`;

    // The Closed column, which used to be an em-dash on every historical row because ClosedAt post-dates
    // most of the ledger. An exact close time is shown when we have one; otherwise the flip's own start is
    // shown prefixed with ~ and explained on hover. Only a flip with neither is a dash.
    const closedCell = p => {
      if (p.closedAgoMinutes != null) return ago(p.closedAgoMinutes);
      if (p.openedAgoMinutes != null)
        return `<span title="closed before the exact time was recorded; this is when the flip opened">`
             + `~${ago(p.openedAgoMinutes)}</span>`;
      return '<span class="muted">&mdash;</span>';
    };

    const top = Math.max(1, ...d.preferences.map(p => Math.abs(p.score)));
    document.querySelector('#prefs tbody').innerHTML = d.preferences.map(p => {
      const pct = Math.max(2, Math.round(Math.abs(p.score) / top * 100));

      // "eligible" on a zero score reads as an endorsement of something that returned nothing. The three
      // states worth telling apart are: proven, tried-and-returned-nothing, and barred. A zero-score entry
      // is a product whose buy order was cancelled unfilled — recorded on purpose, because it tied up
      // capital and returned none, but it is not a preference.
      const status = p.benched ? '<span class="neg">benched</span>'
        : p.score > 0 ? '<span class="pos">favoured</span>'
        : '<span class="warn">no return</span>';

      return `<tr><td>${p.name}</td>`
        + `<td class="num barcell"><span class="bar" style="width:${pct}%"></span><span class="${cls(p.score)}">${coins(p.score)}</span></td>`
        + `<td class="num">${p.trades}</td><td class="num ${cls(p.total)}">${coins(p.total)}</td>`
        + `<td>${status}</td></tr>`;
    }).join('') || '<tr><td colspan="5" class="muted">no trade history yet</td></tr>';

    const sum = d.closed.reduce((a,p) => a + (p.profit || 0), 0);
    // Already newest-first from the server, so no reverse() here — reversing it again buried the newest
    // flip at the bottom, which is the one worth seeing without scrolling.
    document.querySelector('#closed tbody').innerHTML = d.closed.map(p =>
      `<tr><td>${p.name}${p.basisKnown === false ? ' <span class="warn" title="cost basis rebuilt from the order menu — profit is a guess">?</span>' : ''}</td>`
      + `<td class="num">${p.unitsBought}</td><td class="num">${coins(p.spent)}</td>`
      + `<td class="num">${p.unitsSold}</td><td class="num">${coins(p.received)}</td>`
      + `<td class="num ${cls(p.profit)}">${coins(p.profit)}</td>`
      + `<td class="num muted">${closedCell(p)}</td></tr>`).join('')
      || '<tr><td colspan="7" class="muted">none yet</td></tr>';

    // One assignment, not two. The total was being written here and then immediately overwritten by the
    // last-close line, so the figure was computed every tick and never seen. Both belong in the hint.
    // The ~ marks a last-close time derived from when the flip opened, matching how the rows are marked.
    document.getElementById('closedsum').innerHTML = d.closed.length
      ? `<span class="${cls(sum)}">${coins(sum)} total</span> · last close `
        + `${d.lastCloseExact === false ? '~' : ''}${ago(d.lastCloseAgoMinutes)}`
      : '';

    drawSpark(d.pnlSeries || []);

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
