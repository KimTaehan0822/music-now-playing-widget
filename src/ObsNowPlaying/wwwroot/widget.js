(() => {
  const HIDE_DELAY_MS = 1500;
  const params = new URLSearchParams(location.search);
  const urlStyle = /^[ABC]$/.test(params.get('style') || '') ? params.get('style') : null;
  const preview = params.get('preview') === '1';
  const SAMPLE = { playing: true, title: 'Sample Song Title', artist: 'Sample Artist', artVersion: null, positionMs: 72000, durationMs: 200000, positionAtMs: 0, sample: true };

  const w = document.getElementById('w');
  const art = w.querySelector('.art');
  const artImg = art.querySelector('img');
  const backdropImg = w.querySelector('.backdrop img');
  const titleLine = w.querySelector('.title');
  const artistLine = w.querySelector('.artist');
  const barFill = w.querySelector('.bar i');

  let settings = null;
  let state = null;
  let hideTimer = null;
  let lastKey = '';

  function hexToRgb(hex) {
    const n = parseInt(hex.slice(1), 16);
    return `${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}`;
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

  let shown = null;

  function progress() {
    if (!shown || !shown.durationMs || shown.positionMs == null) return;
    const elapsed = shown.playing && !shown.sample ? Date.now() - shown.positionAtMs : 0;
    const pos = Math.min(shown.durationMs, Math.max(0, shown.positionMs + elapsed));
    barFill.style.width = `${(pos / shown.durationMs) * 100}%`;
  }

  function render() {
    if (!settings) return;
    shown = state && state.playing ? state : (preview ? SAMPLE : state);
    const style = urlStyle || settings.defaultStyle;
    const root = document.documentElement.style;
    root.setProperty('--text', settings.textColor);
    root.setProperty('--bg-rgb', hexToRgb(settings.backgroundColor));
    root.setProperty('--accent', settings.accentColor);

    const hasArt = settings.showArt && shown && (shown.artVersion || shown.sample);
    w.className = `w style-${style}`
      + (hasArt ? '' : ' no-art')
      + (settings.showProgress && shown && shown.durationMs ? '' : ' no-progress')
      + (w.classList.contains('hidden') ? ' hidden' : '');

    if (!shown || !shown.playing) { setVisible(false); return; }

    const key = `${shown.title}\n${shown.artist}`;
    titleLine.firstElementChild.textContent = shown.title;
    artistLine.firstElementChild.textContent = shown.artist;
    art.classList.toggle('sample', !!shown.sample);
    if (shown.artVersion) {
      const src = `/art?v=${encodeURIComponent(shown.artVersion)}`;
      if (artImg.getAttribute('src') !== src) { artImg.src = src; backdropImg.src = src; }
      artImg.hidden = backdropImg.hidden = false;
    } else {
      artImg.hidden = backdropImg.hidden = true;
    }
    if (key !== lastKey && lastKey) {
      w.classList.remove('swap');
      void w.offsetWidth; // restart the animation
      w.classList.add('swap');
    }
    lastKey = key;
    setVisible(true);
    progress();
    requestAnimationFrame(() => { fitLine(titleLine); fitLine(artistLine); });
  }

  setInterval(progress, 1000);

  const events = new EventSource('/events');
  let previewOverride = null; // unsaved values from the settings page win over server pushes
  events.addEventListener('settings', e => { settings = Object.assign(JSON.parse(e.data), previewOverride); render(); });
  events.addEventListener('nowplaying', e => { state = JSON.parse(e.data); render(); });
  events.onerror = () => { if (!preview) setVisible(false); }; // EventSource reconnects by itself

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
