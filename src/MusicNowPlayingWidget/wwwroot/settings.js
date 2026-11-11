(() => {
  const $ = id => document.getElementById(id);
  const form = $('form');
  const frame = $('frame');
  const COLOR_IDS = ['textColor', 'backgroundColor', 'accentColor'];
  const SIZES = { A: '430 × 110', B: '450 × 70', C: '560 × 140' }; // same as the README

  let saved = null;      // last settings the server confirmed
let startupOn = null;  // last "start with Windows" state the server confirmed; null until known
  let strings = {};
  let currentLang = null;
  let previewStyle = null;
  let anyPlaying = false;
  let programs = {};          // picked programs being edited: name -> { enabled, mode, order }
  let programsDirty = false;  // edited here and not saved yet: server pushes must not wipe the edits
  let widgetUrl = `${location.origin}/widget`; // replaced by the file:/// address from /api/info

  const t = key => strings[key] || key;

  function resolveLang(setting) {
    if (setting) return setting;
    const nav = (navigator.language || 'en').slice(0, 2);
    return nav === 'ko' || nav === 'ja' ? nav : 'en';
  }

  async function applyLanguage(setting) {
    const lang = resolveLang(setting);
    if (lang === currentLang) return;
    strings = await (await fetch(`/i18n/${lang}.json`)).json();
    currentLang = lang;
    document.documentElement.lang = lang;
    document.querySelectorAll('[data-i18n]').forEach(el => { el.textContent = t(el.dataset.i18n); });
    document.title = t('settings.title');
    renderUrls();
    if (saved) { renderApps(saved.apps, readForm().apps); renderPrograms(); } // app names are translated too
  }

  // "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" -> "Spotify", "C:\\x\\whale.exe" -> "whale"
  // IDs that don't read as the player's name.
  const KNOWN_APPS = {
    'com.kakaoent.melon.player': 'Melon',
    'com.squirrel.TIDAL.TIDAL': 'TIDAL',
    'AppleInc.AppleMusicWin_nzyj5cx40ttqa!App': 'Apple Music',
  };

  function friendlyName(appId) {
    if (KNOWN_APPS[appId]) return KNOWN_APPS[appId];
    // A site installed as an app, e.g. "Chrome._crx_cinhimbnkk…" is YouTube Music installed from Chrome.
    const web = appId.match(/^(.+?)\._crx_(.+)$/);
    if (web) {
      const label = t('settings.webApp').replace('{browser}', web[1]);
      return web[2].startsWith('cinhimbnkk') ? `YouTube Music (${label})` : label;
    }
    const tail = appId.includes('!') ? appId.split('!').pop() : appId.split(/[\\/]/).pop();
    return tail.replace(/\.exe$/i, '') || appId;
  }

  function readForm() {
    const apps = {};
    document.querySelectorAll('#apps input').forEach(cb => { apps[cb.value] = cb.checked; });
    return {
      language: $('language').value || null,
      defaultStyle: form.querySelector('input[name=style]:checked')?.value || 'A',
      textColor: $('textColor').value,
      backgroundColor: $('backgroundColor').value,
      accentColor: $('accentColor').value,
      showArt: $('showArt').checked,
      showProgress: $('showProgress').checked,
      showTime: $('showTime').checked,
      autoColors: $('autoColors').checked,
      apps,
      programs: structuredClone(programs),
    };
  }

  function fillForm(s) {
    $('language').value = s.language || '';
    form.querySelectorAll('input[name=style]').forEach(r => { r.checked = r.value === s.defaultStyle; });
    COLOR_IDS.forEach(id => { $(id).value = s[id].toLowerCase(); });
    $('showArt').checked = s.showArt;
    $('showProgress').checked = s.showProgress;
    $('showTime').checked = s.showTime;
    $('autoColors').checked = s.autoColors;
    renderApps(s.apps, {});
    programs = structuredClone(s.programs || {});
    programsDirty = false;
    renderPrograms();
  }

  // redraw only when the row's shape changes, so keyboard focus stays on a checkbox or select
  function editPrograms(change, redraw = true) {
    change();
    programsDirty = true;
    $('saved').textContent = '';
    if (redraw) renderPrograms();
  }

  function option(value, key, selected) {
    const o = document.createElement('option');
    o.value = value;
    o.textContent = t(key);
    o.selected = value === selected;
    return o;
  }

  // Process names of browsers (same list as the server's browser marks)
  const BROWSERS = /^(chrome|msedge|whale|firefox|opera|brave|vivaldi)$/i;

  function renderPrograms() {
    const list = $('programs');
    list.replaceChildren();
    for (const name of Object.keys(programs).sort((a, b) => a.localeCompare(b))) {
      const p = programs[name];
      const li = document.createElement('li');
      li.className = 'program';
      li.dataset.name = name;
      const row = document.createElement('div');
      row.className = 'row';
      const label = document.createElement('label');
      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.checked = p.enabled;
      cb.addEventListener('change', () => editPrograms(() => { p.enabled = cb.checked; }, false));
      const title = document.createElement('span');
      title.textContent = name;
      label.append(cb, title);

      const controls = document.createElement('div');
      controls.className = 'controls';
      const mode = document.createElement('select');
      mode.dataset.role = 'mode';
      mode.setAttribute('aria-label', t('settings.programMode').replace('{name}', name));
      mode.append(option('windows', 'settings.modeWindows', p.mode), option('title', 'settings.modeTitle', p.mode));
      mode.addEventListener('change', () => {
        editPrograms(() => { p.mode = mode.value; });
        $('programs').querySelector(`li[data-name="${CSS.escape(name)}"] select[data-role=mode]`)?.focus();
      });
      controls.append(mode);
      if (p.mode === 'title') {
        const order = document.createElement('select');
        order.setAttribute('aria-label', t('settings.programOrder').replace('{name}', name));
        order.append(option('title-artist', 'settings.orderTitleArtist', p.order),
          option('artist-title', 'settings.orderArtistTitle', p.order));
        order.addEventListener('change', () => editPrograms(() => { p.order = order.value; }, false));
        controls.append(order);
      }
      const remove = document.createElement('button');
      remove.type = 'button';
      remove.className = 'remove';
      remove.textContent = t('settings.removeProgram');
      remove.addEventListener('click', () => editPrograms(() => { delete programs[name]; }));
      controls.append(remove);
      row.append(label, controls);

      const hint = document.createElement('p');
      hint.className = 'hint program-hint';
      hint.textContent = t(p.mode === 'title' ? 'settings.titleModeHint' : 'settings.windowsModeHint');
      li.append(row, hint);
      if (p.mode === 'title' && BROWSERS.test(name)) {
        const warn = document.createElement('p');
        warn.className = 'hint program-hint warn';
        warn.textContent = t('settings.titleBrowserWarning');
        li.append(warn);
      }
      list.append(li);
    }
  }

  async function togglePicker() {
    const picker = $('programPicker');
    const btn = $('addProgram');
    if (!picker.hidden) { picker.hidden = true; btn.setAttribute('aria-expanded', 'false'); return; }
    let names = [];
    try { names = await (await fetch('/api/programs')).json(); } catch { /* shown as "none" below */ }
    const taken = new Set(Object.keys(programs).map(n => n.toLowerCase()));
    names = names.filter(n => !taken.has(n.toLowerCase()));
    picker.replaceChildren();
    if (!names.length) {
      const none = document.createElement('p');
      none.className = 'hint';
      none.textContent = t('settings.programsNone');
      picker.append(none);
    }
    for (const name of names) {
      const b = document.createElement('button');
      b.type = 'button';
      b.textContent = name;
      b.addEventListener('click', () => {
        editPrograms(() => { programs[name] = { enabled: true, mode: 'windows', order: 'title-artist' }; });
        picker.hidden = true;
        btn.setAttribute('aria-expanded', 'false');
        btn.focus();
      });
      picker.append(b);
    }
    picker.hidden = false;
    btn.setAttribute('aria-expanded', 'true');
  }
  $('addProgram').addEventListener('click', togglePicker);

  function renderApps(apps, unsaved) {
    const list = $('apps');
    list.replaceChildren();
    const ids = Object.keys(apps).sort((a, b) => friendlyName(a).localeCompare(friendlyName(b)));
    for (const id of ids) {
      const li = document.createElement('li');
      const label = document.createElement('label');
      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.value = id;
      cb.checked = id in unsaved ? unsaved[id] : apps[id];
      const name = document.createElement('span');
      name.textContent = friendlyName(id);
      const raw = document.createElement('span');
      raw.className = 'id';
      raw.textContent = id;
      raw.title = id;
      label.append(cb, name, raw);
      li.append(label);
      list.append(li);
    }
    $('appsEmpty').hidden = ids.length > 0;
  }

  function renderUrls() {
    const list = $('urls');
    list.replaceChildren();
    for (const style of ['A', 'B', 'C']) {
      const url = `${widgetUrl}?style=${style}`;
      const li = document.createElement('li');
      const name = document.createElement('span');
      name.className = 'name';
      name.textContent = t(`settings.style${style}`);
      const size = document.createElement('small');
      size.textContent = SIZES[style]; // OBS browser source width × height
      name.append(size);
      const code = document.createElement('code');
      code.textContent = url;
      const btn = document.createElement('button');
      btn.type = 'button';
      btn.textContent = t('settings.copy');
      btn.addEventListener('click', async () => {
        await navigator.clipboard.writeText(url);
        btn.textContent = t('settings.copied');
        setTimeout(() => { btn.textContent = t('settings.copy'); }, 1500);
      });
      li.append(name, code, btn);
      list.append(li);
    }
  }

  function updatePreview() {
    const s = readForm();
    document.documentElement.style.setProperty('--accent', s.accentColor);
    COLOR_IDS.forEach(id => { $(id).parentElement.querySelector('code').textContent = $(id).value.toUpperCase(); });
    if (previewStyle !== s.defaultStyle) {
      previewStyle = s.defaultStyle;
      frame.src = `/widget?style=${s.defaultStyle}&preview=1`;
      frame.onload = () => frame.contentWindow.postMessage({ type: 'preview-settings', settings: readForm() }, location.origin);
    } else {
      frame.contentWindow?.postMessage({ type: 'preview-settings', settings: s }, location.origin);
    }
    $('sampleNote').hidden = anyPlaying;
  }

  form.addEventListener('input', () => { $('saved').textContent = ''; updatePreview(); });
  $('language').addEventListener('change', () => { $('saved').textContent = ''; applyLanguage($('language').value || null); });

  form.addEventListener('submit', async e => {
    e.preventDefault();
    const res = await fetch('/api/settings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(readForm()),
    });
    if (!res.ok) { $('saved').textContent = `Error ${res.status}`; return; }
    saved = await res.json();
    fillForm(saved);
    updatePreview();
    // Lives in the Windows registry, not settings.json. Sent only when the user changed it,
    // so a stale or half-loaded tab never overwrites it; the reply is what Windows actually holds.
    const wanted = $('startWithWindows').checked;
    if (startupOn !== null && wanted !== startupOn) {
      try {
        const r = await fetch('/api/startup', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ enabled: wanted }),
        });
        if (r.ok) startupOn = (await r.json()).enabled;
        $('startWithWindows').checked = startupOn;
        if (!r.ok || startupOn !== wanted) { $('saved').textContent = `Error ${r.status}`; return; }
      } catch {
        $('startWithWindows').checked = startupOn;
        $('saved').textContent = 'Error';
        return;
      }
    }
    $('saved').textContent = t('settings.saved');
  });

  fetch('/api/startup').then(r => r.json()).then(s => {
    startupOn = s.enabled;
    $('startWithWindows').checked = s.enabled;
  }).catch(() => { $('startWithWindows').disabled = true; }); // unknown state: don't let Save guess
  fetch('/api/info').then(r => r.json()).then(info => { widgetUrl = info.widgetUrl; renderUrls(); });

  const events = new EventSource('/events');
  events.addEventListener('settings', async e => {
    const next = JSON.parse(e.data);
    if (!saved) {
      saved = next;
      await applyLanguage(next.language);
      fillForm(next);
      updatePreview();
      return;
    }
    // A new app was seen (or another tab saved): refresh the list, keep unsaved ticks.
    const unsaved = readForm().apps;
    saved = next;
    renderApps(next.apps, unsaved);
    // A picked row may have joined the app list on the server; only redraw when nothing here is unsaved.
    if (!programsDirty) { programs = structuredClone(next.programs || {}); renderPrograms(); }
  });
  events.addEventListener('nowplaying', e => {
    anyPlaying = JSON.parse(e.data).playing;
    $('sampleNote').hidden = anyPlaying;
  });
})();
