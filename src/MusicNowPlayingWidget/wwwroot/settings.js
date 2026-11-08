(() => {
  const $ = id => document.getElementById(id);
  const form = $('form');
  const frame = $('frame');
  const COLOR_IDS = ['textColor', 'backgroundColor', 'accentColor'];
  const SIZES = { A: '430 × 110', B: '450 × 70', C: '560 × 140' }; // same as the README

  let saved = null;      // last settings the server confirmed
  let strings = {};
  let currentLang = null;
  let previewStyle = null;
  let anyPlaying = false;
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
    if (saved) renderApps(saved.apps, readForm().apps); // app names are translated too
  }

  // "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" -> "Spotify", "C:\\x\\whale.exe" -> "whale"
  function friendlyName(appId) {
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
  }

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
    $('saved').textContent = t('settings.saved');
  });

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
  });
  events.addEventListener('nowplaying', e => {
    anyPlaying = JSON.parse(e.data).playing;
    $('sampleNote').hidden = anyPlaying;
  });
})();
