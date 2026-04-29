namespace naija_shield_backend.Endpoints;

public static class DemoPageEndpoints
{
    public static void MapDemoPageEndpoints(this WebApplication app)
    {
        app.MapGet("/demo/call", () => Results.Content(Html, "text/html")).AllowAnonymous();
    }

    private const string Html = """
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>NaijaShield — Live Voice Interception Demo</title>
          <style>
            *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
            :root {
              --bg:        #050d1a;
              --card:      #0c1a30;
              --border:    #1a3050;
              --green:     #00c851;
              --red:       #ff3b30;
              --orange:    #ff9500;
              --blue:      #2979ff;
              --text:      #dde6f5;
              --muted:     #7a92b0;
              --radius:    12px;
            }
            body {
              background: var(--bg);
              color: var(--text);
              font-family: 'Segoe UI', system-ui, sans-serif;
              min-height: 100vh;
              padding: 24px 16px 60px;
            }

            /* ── Header ── */
            .header { text-align: center; margin-bottom: 32px; }
            .logo { font-size: 2rem; font-weight: 800; letter-spacing: -1px; }
            .logo span { color: var(--green); }
            .tagline { color: var(--muted); font-size: .9rem; margin-top: 4px; }
            .live-badge {
              display: inline-flex; align-items: center; gap: 6px;
              background: rgba(255,59,48,.15); border: 1px solid var(--red);
              color: var(--red); border-radius: 999px;
              padding: 3px 12px; font-size: .75rem; font-weight: 700;
              margin-top: 10px; letter-spacing: .5px;
            }
            .pulse { width: 7px; height: 7px; border-radius: 50%; background: var(--red);
              animation: pulse 1.2s ease-in-out infinite; }
            @keyframes pulse { 0%,100%{opacity:1} 50%{opacity:.2} }

            /* ── Controls ── */
            .controls {
              display: flex; gap: 10px; flex-wrap: wrap;
              max-width: 760px; margin: 0 auto 28px;
            }
            select, button {
              border-radius: 8px; border: 1px solid var(--border);
              padding: 10px 16px; font-size: .9rem; cursor: pointer;
            }
            select { flex: 1; background: var(--card); color: var(--text); }
            button {
              background: var(--green); color: #000; font-weight: 700;
              border-color: var(--green); transition: opacity .2s;
              white-space: nowrap;
            }
            button:hover { opacity: .85; }
            button:disabled { opacity: .4; cursor: not-allowed; }

            /* ── Main grid ── */
            .grid {
              display: grid;
              grid-template-columns: 1fr 1fr;
              gap: 16px;
              max-width: 760px;
              margin: 0 auto;
            }
            @media(max-width:600px){ .grid { grid-template-columns: 1fr; } }

            .card {
              background: var(--card); border: 1px solid var(--border);
              border-radius: var(--radius); padding: 20px;
            }
            .card-title {
              font-size: .7rem; font-weight: 700; letter-spacing: 1.2px;
              text-transform: uppercase; color: var(--muted); margin-bottom: 14px;
            }

            /* ── Threat card ── */
            .number { font-size: 1.3rem; font-weight: 700; font-family: monospace; margin-bottom: 6px; }
            .channel-badge {
              display: inline-block; font-size: .7rem; font-weight: 700;
              background: rgba(41,121,255,.2); border: 1px solid var(--blue);
              color: var(--blue); border-radius: 4px; padding: 2px 8px;
              text-transform: uppercase; letter-spacing: .5px;
            }
            .risk-row { display: flex; align-items: center; gap: 10px; margin-top: 14px; }
            .risk-circle {
              width: 62px; height: 62px; border-radius: 50%;
              border: 3px solid var(--red); display: flex;
              flex-direction: column; align-items: center; justify-content: center;
              font-weight: 800; font-size: 1.3rem; color: var(--red);
              flex-shrink: 0; transition: border-color .4s, color .4s;
            }
            .risk-circle small { font-size: .5rem; font-weight: 400; color: var(--muted); }
            .verdict-pill {
              padding: 6px 14px; border-radius: 999px; font-weight: 700;
              font-size: .8rem; letter-spacing: .5px; text-transform: uppercase;
            }
            .verdict-blocked  { background: rgba(255,59,48,.2);  color: var(--red);    border: 1px solid var(--red); }
            .verdict-monitor  { background: rgba(255,149,0,.2);  color: var(--orange); border: 1px solid var(--orange); }
            .verdict-allowed  { background: rgba(0,200,81,.2);   color: var(--green);  border: 1px solid var(--green); }
            .verdict-waiting  { background: rgba(122,146,176,.1); color: var(--muted);  border: 1px solid var(--border); }

            /* ── Pipeline ── */
            .step {
              display: flex; align-items: center; gap: 10px;
              padding: 7px 0; border-bottom: 1px solid var(--border);
              opacity: 0; transition: opacity .3s;
            }
            .step.visible { opacity: 1; }
            .step-icon { width: 20px; height: 20px; border-radius: 50%; flex-shrink: 0;
              display: flex; align-items: center; justify-content: center; font-size: .75rem; }
            .step-icon.ok  { background: rgba(0,200,81,.2); color: var(--green); }
            .step-icon.spin { background: rgba(41,121,255,.15); animation: spin-border 1s linear infinite; }
            @keyframes spin-border { to { transform: rotate(360deg); } }
            .step-name { flex: 1; font-size: .82rem; }
            .step-ms { font-size: .75rem; color: var(--muted); font-family: monospace; }
            .step-detail { font-size: .7rem; color: var(--muted); margin-left: 30px; padding-bottom: 6px; }

            /* ── Call panel (full width) ── */
            .call-panel {
              grid-column: 1 / -1;
              display: none;
            }
            .call-panel.visible { display: block; }
            .phone-ui {
              background: #08162b;
              border: 1px solid var(--green);
              border-radius: 24px;
              padding: 28px 24px;
              text-align: center;
              max-width: 340px;
              margin: 0 auto;
              position: relative;
              overflow: hidden;
            }
            .phone-ui::before {
              content: '';
              position: absolute; inset: 0;
              background: radial-gradient(ellipse at 50% -20%, rgba(0,200,81,.08) 0%, transparent 70%);
              pointer-events: none;
            }
            .caller-label { font-size: .7rem; color: var(--muted); letter-spacing: 1px; text-transform: uppercase; }
            .caller-name { font-size: 1.4rem; font-weight: 800; color: var(--green); margin: 6px 0 2px; }
            .caller-num { font-family: monospace; color: var(--muted); font-size: .85rem; margin-bottom: 20px; }
            .call-status { font-size: .85rem; color: var(--muted); margin-bottom: 16px; min-height: 1.3em; }

            /* Rings */
            .rings { position: relative; width: 100px; height: 100px; margin: 0 auto 20px; }
            .ring {
              position: absolute; border-radius: 50%; border: 2px solid var(--green);
              opacity: 0; animation: ring-out 2s ease-out infinite;
            }
            .ring:nth-child(1) { inset: 30px; }
            .ring:nth-child(2) { inset: 15px; animation-delay: .5s; }
            .ring:nth-child(3) { inset: 0;    animation-delay: 1s; }
            .ring-icon {
              position: absolute; inset: 30px; background: var(--green);
              border-radius: 50%; display: flex; align-items: center;
              justify-content: center; font-size: 1.5rem;
            }
            @keyframes ring-out {
              0%   { opacity: .9; transform: scale(.8); }
              100% { opacity: 0;  transform: scale(1.6); }
            }

            /* Waveform */
            .waveform { display: flex; align-items: center; justify-content: center;
              gap: 4px; height: 48px; margin: 14px 0; display: none; }
            .waveform.playing { display: flex; }
            .bar {
              width: 4px; border-radius: 2px; background: var(--green);
              animation: wave 1s ease-in-out infinite;
            }
            .bar:nth-child(2) { animation-delay: .1s; }
            .bar:nth-child(3) { animation-delay: .2s; }
            .bar:nth-child(4) { animation-delay: .3s; }
            .bar:nth-child(5) { animation-delay: .4s; }
            .bar:nth-child(6) { animation-delay: .3s; }
            .bar:nth-child(7) { animation-delay: .2s; }
            .bar:nth-child(8) { animation-delay: .1s; }
            @keyframes wave {
              0%,100% { height: 8px; }
              50%      { height: 36px; }
            }

            .warning-text {
              font-size: .78rem; color: var(--muted); line-height: 1.5;
              border-top: 1px solid var(--border); padding-top: 14px; margin-top: 10px;
              display: none;
            }
            .warning-text.visible { display: block; }

            .hang-up-badge {
              display: none; margin-top: 14px;
              background: rgba(0,200,81,.1); border: 1px solid var(--green);
              border-radius: 8px; padding: 8px 14px;
              font-size: .82rem; color: var(--green); font-weight: 600;
            }
            .hang-up-badge.visible { display: block; }

            /* ── Incident card ── */
            .incident-card { grid-column: 1 / -1; display: none; }
            .incident-card.visible { display: block; }
            .kv { display: flex; justify-content: space-between; padding: 6px 0;
              border-bottom: 1px solid var(--border); font-size: .82rem; }
            .kv:last-child { border-bottom: none; }
            .kv-key { color: var(--muted); }
            .kv-val { font-family: monospace; font-weight: 600; max-width: 55%; text-align: right; word-break: break-all; }

            /* ── Footer status ── */
            #status-bar {
              text-align: center; font-size: .8rem; color: var(--muted);
              margin-top: 24px; max-width: 760px; margin-left: auto; margin-right: auto;
            }
          </style>
        </head>
        <body>

          <div class="header">
            <div class="logo">Naija<span>Shield</span></div>
            <div class="tagline">Real-time Telecoms Fraud Interception &amp; Victim Protection</div>
            <div class="live-badge"><div class="pulse"></div>LIVE PIPELINE</div>
          </div>

          <div class="controls">
            <select id="script-select">
              <option value="en-vishing-cbn">🇳🇬 English — CBN Vishing (impersonating CBN fraud unit)</option>
              <option value="en-family-impersonation">🇳🇬 English — Family Impersonation (bail scam)</option>
              <option value="en-otp-gtbank">🇳🇬 English — GTBank OTP Phish</option>
              <option value="pidgin-otp-social">🇳🇬 Pidgin — Social Engineering OTP</option>
              <option value="pidgin-ussd-trick">🇳🇬 Pidgin — USSD Reversal Trick</option>
              <option value="yoruba-otp-gtbank">🇳🇬 Yoruba — GTBank OTP Phish</option>
              <option value="hausa-otp-zenith">🇳🇬 Hausa — Zenith Bank OTP</option>
              <option value="igbo-otp-uba">🇳🇬 Igbo — UBA OTP Phish</option>
            </select>
            <button id="run-btn" onclick="runDemo()">▶ Run Demo</button>
          </div>

          <div class="grid">

            <!-- Threat card -->
            <div class="card">
              <div class="card-title">⚠ Incoming Threat</div>
              <div id="from-number" class="number" style="color:var(--muted)">Waiting...</div>
              <span id="channel-badge" class="channel-badge" style="visibility:hidden">SMS</span>
              <div class="risk-row">
                <div class="risk-circle" id="risk-circle">
                  <span id="risk-score">—</span>
                  <small>RISK</small>
                </div>
                <div>
                  <div id="classification" style="font-size:.8rem;color:var(--muted);margin-bottom:6px">—</div>
                  <div id="verdict-pill" class="verdict-pill verdict-waiting">PENDING</div>
                </div>
              </div>
            </div>

            <!-- Pipeline card -->
            <div class="card">
              <div class="card-title">⚙ Detection Pipeline</div>
              <div id="pipeline-steps"></div>
              <div id="pipeline-idle" style="color:var(--muted);font-size:.82rem">
                Pipeline will run when you click Run Demo.
              </div>
            </div>

            <!-- Call panel (full width) -->
            <div class="card call-panel" id="call-panel">
              <div class="card-title">📞 NaijaShield Warning Call</div>
              <div class="phone-ui">
                <div class="caller-label">INCOMING CALL FROM</div>
                <div class="caller-name">🛡 NaijaShield</div>
                <div class="caller-num">+234-NAIJA-SHIELD</div>
                <div class="rings">
                  <div class="ring"></div>
                  <div class="ring"></div>
                  <div class="ring"></div>
                  <div class="ring-icon">📞</div>
                </div>
                <div class="call-status" id="call-status">Ringing victim...</div>
                <div class="waveform" id="waveform">
                  <div class="bar"></div><div class="bar"></div><div class="bar"></div>
                  <div class="bar"></div><div class="bar"></div><div class="bar"></div>
                  <div class="bar"></div><div class="bar"></div>
                </div>
                <div class="warning-text" id="warning-text"></div>
                <div class="hang-up-badge" id="hang-up-badge">✓ Call complete — victim warned and scammer blocked</div>
              </div>
            </div>

            <!-- Incident summary (full width) -->
            <div class="card incident-card" id="incident-card">
              <div class="card-title">🗂 Incident Record — Saved to Cosmos DB</div>
              <div id="incident-kv"></div>
            </div>

          </div>

          <div id="status-bar">Select a scam script and click Run Demo to start.</div>

          <script>
            const API = '';  // same origin

            let running = false;

            function setStatus(msg) {
              document.getElementById('status-bar').textContent = msg;
            }

            function delay(ms) {
              return new Promise(r => setTimeout(r, ms));
            }

            function stepIcon(name) {
              const icons = {
                input:              '📥',
                piiRedaction:       '🔒',
                llmScoring:         '🤖',
                statusDetermination:'⚖',
                geolocation:        '📍',
                cosmosDb:           '💾',
                signalRBroadcast:   '📡',
                intervention:       '🚨',
              };
              return icons[name] || '▸';
            }

            function stepLabel(name) {
              const labels = {
                input:               'Input resolved',
                piiRedaction:        'PII redacted',
                llmScoring:          'AI threat scoring',
                statusDetermination: 'Status determined',
                geolocation:         'Geolocation lookup',
                cosmosDb:            'Saved to Cosmos DB',
                signalRBroadcast:    'Dashboard updated (SignalR)',
                intervention:        'Intervention',
              };
              return labels[name] || name;
            }

            function scoreColor(score) {
              if (score >= 85) return 'var(--red)';
              if (score >= 50) return 'var(--orange)';
              return 'var(--green)';
            }

            async function runDemo() {
              if (running) return;
              running = true;

              const btn      = document.getElementById('run-btn');
              const scriptId = document.getElementById('script-select').value;
              btn.disabled   = true;
              btn.textContent = '⏳ Running...';

              // Reset UI
              document.getElementById('pipeline-steps').innerHTML = '';
              document.getElementById('pipeline-idle').style.display = 'none';
              document.getElementById('from-number').textContent = 'Detecting...';
              document.getElementById('from-number').style.color = 'var(--muted)';
              document.getElementById('channel-badge').style.visibility = 'hidden';
              document.getElementById('risk-score').textContent = '—';
              document.getElementById('classification').textContent = '—';
              document.getElementById('verdict-pill').className = 'verdict-pill verdict-waiting';
              document.getElementById('verdict-pill').textContent = 'ANALYZING';
              document.getElementById('call-panel').classList.remove('visible');
              document.getElementById('incident-card').classList.remove('visible');
              document.getElementById('waveform').classList.remove('playing');
              document.getElementById('warning-text').classList.remove('visible');
              document.getElementById('hang-up-badge').classList.remove('visible');
              document.getElementById('call-status').textContent = 'Ringing victim...';

              setStatus('Calling detection pipeline…');

              // ── 1. Call the real simulate endpoint ──────────────────────
              let data;
              try {
                const res = await fetch(`${API}/api/demo/simulate-scam-call`, {
                  method:  'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body:    JSON.stringify({ scriptId })
                });
                if (!res.ok) throw new Error(`API returned ${res.status}`);
                data = await res.json();
              } catch (err) {
                setStatus('Error: ' + err.message);
                btn.disabled = false;
                btn.textContent = '▶ Run Demo';
                running = false;
                return;
              }

              // ── 2. Animate pipeline steps ────────────────────────────────
              const container = document.getElementById('pipeline-steps');
              for (const step of data.pipeline) {
                const row = document.createElement('div');
                row.className = 'step';

                let detail = '';
                if (step.step === 'llmScoring') {
                  detail = `Score: ${step.result.RiskScore ?? step.result.riskScore} · ${step.result.Classification ?? step.result.classification} · ${step.result.Confidence ?? step.result.confidence}`;
                } else if (step.step === 'geolocation') {
                  detail = `${step.result.state || '?'}, ${step.result.lga || '?'}`;
                } else if (step.step === 'cosmosDb') {
                  detail = `ID: ${step.result.saved}`;
                }

                row.innerHTML = `
                  <div class="step-icon ok">${stepIcon(step.step)}</div>
                  <span class="step-name">${stepLabel(step.step)}</span>
                  <span class="step-ms">${step.elapsedMs}ms</span>
                `;
                container.appendChild(row);
                if (detail) {
                  const d = document.createElement('div');
                  d.className = 'step-detail';
                  d.textContent = detail;
                  container.appendChild(d);
                }
                await delay(10);
                row.classList.add('visible');
                await delay(320);
              }

              // ── 3. Update threat card ────────────────────────────────────
              const v = data.verdict;
              const inc = data.incident;

              document.getElementById('from-number').textContent = inc.from || inc.From;
              document.getElementById('from-number').style.color = 'var(--text)';

              const ch = document.getElementById('channel-badge');
              ch.textContent = inc.channel || inc.Channel;
              ch.style.visibility = 'visible';

              const score = v.riskScore;
              const circle = document.getElementById('risk-circle');
              circle.style.borderColor = scoreColor(score);
              circle.style.color       = scoreColor(score);
              document.getElementById('risk-score').textContent = score;
              document.getElementById('classification').textContent = v.classification;

              const pill = document.getElementById('verdict-pill');
              pill.textContent = v.status.toUpperCase();
              pill.className   = 'verdict-pill ' + (
                v.status === 'Blocked'    ? 'verdict-blocked' :
                v.status === 'Monitoring' ? 'verdict-monitor'  :
                                            'verdict-allowed'
              );

              // ── 4. Incident card ─────────────────────────────────────────
              const kvContainer = document.getElementById('incident-kv');
              kvContainer.innerHTML = '';
              const kvPairs = [
                ['Incident ID',   inc.id || inc.Id],
                ['Timestamp',     (inc.timestamp || inc.Timestamp || '').replace('T',' ').split('.')[0] + ' UTC'],
                ['From',          inc.from || inc.From],
                ['Channel',       inc.channel || inc.Channel],
                ['Risk Score',    score + ' / 100'],
                ['Classification',v.classification],
                ['Language',      v.detectedLanguage],
                ['Confidence',    v.confidence],
                ['Status',        v.status],
                ['Location',      `${inc.state || inc.State || '?'}, ${inc.lga || inc.Lga || '?'}`],
              ];
              for (const [k, val] of kvPairs) {
                kvContainer.innerHTML += `
                  <div class="kv">
                    <span class="kv-key">${k}</span>
                    <span class="kv-val">${val ?? '—'}</span>
                  </div>`;
              }
              document.getElementById('incident-card').classList.add('visible');

              // ── 5. Voice warning call simulation ────────────────────────
              if (v.status === 'Blocked' || v.status === 'Monitoring') {
                setStatus('Scam detected. Initiating victim warning call via NaijaShield voice system…');
                await delay(800);

                document.getElementById('call-panel').classList.add('visible');
                document.getElementById('call-status').textContent = 'Ringing victim…';

                await delay(2200);  // let the rings animate

                document.getElementById('call-status').textContent = 'Connected — playing warning…';
                document.getElementById('waveform').classList.add('playing');

                const warningText = data.warningTexts?.voice || '';
                if (warningText) {
                  document.getElementById('warning-text').textContent = '"' + warningText + '"';
                  document.getElementById('warning-text').classList.add('visible');
                }

                // Fetch and play real TTS audio
                setStatus('Synthesising warning audio via Azure TTS (Nigerian-English neural voice)…');
                try {
                  const lang = v.detectedLanguage || 'en';
                  const audioRes = await fetch(`${API}/api/demo/voice-warning?lang=${lang}`);
                  if (audioRes.ok) {
                    const blob = await audioRes.blob();
                    const url  = URL.createObjectURL(blob);
                    const audio = new Audio(url);
                    audio.onended = () => {
                      document.getElementById('waveform').classList.remove('playing');
                      document.getElementById('hang-up-badge').classList.add('visible');
                      document.getElementById('call-status').textContent = 'Call ended.';
                      setStatus('✓ Demo complete. Victim warned. Incident saved to Cosmos DB. Dashboard updated via SignalR.');
                      btn.disabled = false;
                      btn.textContent = '▶ Run Again';
                      running = false;
                    };
                    await audio.play();
                    setStatus('▶ Playing warning call audio…');
                  } else {
                    throw new Error(`TTS returned ${audioRes.status}`);
                  }
                } catch (e) {
                  setStatus('Audio error: ' + e.message + ' — check Azure-Speech-Key config');
                  document.getElementById('waveform').classList.remove('playing');
                  document.getElementById('hang-up-badge').classList.add('visible');
                  document.getElementById('call-status').textContent = 'Call complete (audio unavailable in this env).';
                  btn.disabled = false;
                  btn.textContent = '▶ Run Again';
                  running = false;
                }

              } else {
                setStatus('✓ Message scored below threshold — no intervention. Incident recorded.');
                btn.disabled = false;
                btn.textContent = '▶ Run Again';
                running = false;
              }
            }
          </script>
        </body>
        </html>
        """;
}
