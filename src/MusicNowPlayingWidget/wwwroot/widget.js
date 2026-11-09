(() => {
  const HIDE_DELAY_MS = 1500;
  const RECONNECT_MS = 3000;
  const ART_WAIT_MS = 5000; // same as the server's ArtSettleTime
  const FADE_MS = 450;      // matches the .fading transition in widget.css
  const RAPID_MS = 800;     // a song change sooner than this after the last one swaps at once
  const params = new URLSearchParams(location.search);
  const urlStyle = /^[ABC]$/.test(params.get('style') || '') ? params.get('style') : null;
  const preview = params.get('preview') === '1';
  // Opened as a file (file:///...) the page still loads when the app is off, then connects once it starts.
  const server = location.protocol === 'http:' ? '' : `http://localhost:${window.ONP_PORT || 8976}`;
  const SAMPLE = { playing: true, title: 'Sample Song Title', artist: 'Sample Artist', artVersion: null, positionMs: 72000, durationMs: 200000, positionAtMs: 0, sample: true };

  const w = document.getElementById('w');
  const art = w.querySelector('.art');
  const artImg = art.querySelector('img');
  const backdropImg = w.querySelector('.backdrop img');
  const titleLine = w.querySelector('.title');
  const artistLine = w.querySelector('.artist');
  const barFill = w.querySelector('.bar i');
  const timeText = w.querySelector('.time');

  let settings = null;
  let state = null;
  let shown = null;
  let hideTimer = null;
  let lastKey = '';
  let lastPalette = null;
  let keyChangedAt = 0;
  let artWaitTimer = null;
  let fadeTimer = null;
  let previewOverride = null; // unsaved values from the settings page win over server pushes

  function hexToRgb(hex) {
    const n = parseInt(hex.slice(1), 16);
    return `${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}`;
  }

  function clock(ms) {
    const s = Math.floor(ms / 1000);
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const ss = String(s % 60).padStart(2, '0');
    return h ? `${h}:${String(m).padStart(2, '0')}:${ss}` : `${m}:${ss}`;
  }

  function fitLine(line) {
    const span = line.firstElementChild;
    line.classList.remove('scroll');
    const overflow = span.scrollWidth - line.clientWidth;
    if (overflow > 2) {
      line.style.setProperty('--shift', `-${overflow + 8}px`);
      line.style.setProperty('--dur', `${Math.max(8, overflow / 25 + 6)}s`);
      line.classList.add('scroll');
    }
  }

  function setVisible(visible) {
    if (visible) {
      clearTimeout(hideTimer);
      hideTimer = null;
      w.classList.remove('hidden');
    } else if (!hideTimer && !w.classList.contains('hidden')) {
      // Track changes briefly report "paused"; wait before hiding.
      hideTimer = setTimeout(() => { w.classList.add('hidden'); hideTimer = null; }, HIDE_DELAY_MS);
    }
  }

  function progress() {
    if (!shown || !shown.durationMs || shown.positionMs == null) return;
    const elapsed = shown.playing && !shown.sample ? Date.now() - shown.positionAtMs : 0;
    const pos = Math.min(shown.durationMs, Math.max(0, shown.positionMs + elapsed));
    barFill.style.width = `${(pos / shown.durationMs) * 100}%`;
    timeText.textContent = `${clock(pos)} / ${clock(shown.durationMs)}`;
  }

  function render() {
    if (!settings) return;
    const next = state && state.playing ? state : (preview ? SAMPLE : null);
    if (!next) {
      // Keep showing the last track exactly as it was while it fades out.
      setVisible(false);
      return;
    }
    shown = next;
    const style = urlStyle || settings.defaultStyle;
    const root = document.documentElement.style;
    const key = `${shown.title}\n${shown.artist}`;
    const changed = key !== lastKey;
    const sinceLastChange = Date.now() - keyChangedAt;
    if (changed) keyChangedAt = Date.now();
    // Auto colors: the server picked them from the cover. Style C has no box, so its text keeps the user's color.
    // Some players send the cover a moment after the title: keep the old colors meanwhile instead of fading twice.
    if (shown.palette) lastPalette = shown.palette;
    const waitingForArt = !shown.palette && !shown.artVersion && !shown.sample && Date.now() - keyChangedAt < ART_WAIT_MS;
    if (waitingForArt) { clearTimeout(artWaitTimer); artWaitTimer = setTimeout(render, ART_WAIT_MS); }
    const p = settings.autoColors && (shown.palette || (waitingForArt && lastPalette));
    const text = p && style !== 'C' ? p.text : settings.textColor;
    root.setProperty('--text', text);
    root.setProperty('--text-rgb', hexToRgb(text));
    root.setProperty('--bg-rgb', hexToRgb(p ? p.background : settings.backgroundColor));
    root.setProperty('--accent', p ? p.accent : settings.accentColor);

    const hasArt = settings.showArt && (shown.artVersion || shown.sample);
    const hasDuration = !!shown.durationMs;
    w.className = `w style-${style}`
      + (p ? ' auto' : '')
      + (hasArt ? '' : ' no-art')
      + (settings.showProgress && hasDuration ? '' : ' no-progress')
      + (settings.showTime !== false && hasDuration ? '' : ' no-time')
      + (w.classList.contains('hidden') ? ' hidden' : '')
      + (fadeTimer ? ' fading' : '');

    if (!changed) {
      if (!fadeTimer) showContent(); // same song (e.g. its cover arrived); mid-fade the timer shows it
    } else if (!lastKey || w.classList.contains('hidden')) {
      showContent(); // nothing on screen to fade from
    } else if (fadeTimer || sinceLastChange < RAPID_MS) {
      // Skipping fast: swap at once, as a fade would lag behind.
      clearTimeout(fadeTimer);
      fadeTimer = null;
      w.classList.remove('fading');
      showContent();
      w.classList.remove('swap');
      void w.offsetWidth; // restart the animation
      w.classList.add('swap');
    } else {
      w.classList.add('fading'); // fade the old song out, then the new one in
      fadeTimer = setTimeout(() => { fadeTimer = null; showContent(); w.classList.remove('fading'); }, FADE_MS);
    }
    lastKey = key;
    setVisible(true);
    progress();
  }

  function showContent() {
    titleLine.firstElementChild.textContent = shown.title;
    artistLine.firstElementChild.textContent = shown.artist;
    art.classList.toggle('sample', !!shown.sample);
    if (shown.artVersion) {
      const src = `${server}/art?v=${encodeURIComponent(shown.artVersion)}`;
      if (artImg.getAttribute('src') !== src) { artImg.src = src; backdropImg.src = src; }
      artImg.hidden = backdropImg.hidden = false;
    } else {
      artImg.hidden = backdropImg.hidden = true;
    }
    requestAnimationFrame(() => { fitLine(titleLine); fitLine(artistLine); });
  }

  setInterval(progress, 1000);

  function connect() {
    const events = new EventSource(`${server}/events`);
    events.addEventListener('settings', e => { settings = Object.assign(JSON.parse(e.data), previewOverride); render(); });
    events.addEventListener('nowplaying', e => { state = JSON.parse(e.data); render(); });
    // The app was updated while OBS kept this page open: reload once to pick up the new widget files.
    events.addEventListener('build', e => {
      const build = JSON.parse(e.data);
      if (!window.ONP_BUILD || build === window.ONP_BUILD) return;
      // The mark in the address stops a reload loop if the files on disk could not be updated (no storage needed).
      const mark = `#reloaded-${build}`;
      if (location.hash === mark) return;
      location.replace(location.href.split('#')[0] + mark);
      location.reload();
    });
    events.onerror = () => {
      if (!preview) { state = null; render(); }
      // The browser retries by itself unless it gave up (CLOSED); then start over.
      if (events.readyState === EventSource.CLOSED) setTimeout(connect, RECONNECT_MS);
    };
  }
  connect();

  // The settings page previews unsaved changes by posting them into this frame.
  window.addEventListener('message', e => {
    if (e.origin === location.origin && e.data && e.data.type === 'preview-settings') {
      previewOverride = e.data.settings;
      settings = Object.assign({}, settings, previewOverride);
      render();
    }
  });

  window.render = (s, st) => { if (s) settings = s; if (st) state = st; render(); }; // for manual checks
})();
