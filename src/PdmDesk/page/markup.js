// PdfMarkup: comment on a PDF or picture in the browser (Grant, 28 Nov). One file, no libraries: the server renders each
// page to an image and writes the mark-ups into the PDF as standard annotations (Square, Line with an arrow head,
// Highlight, FreeText), so PDF-XChange shows them and desk.py markup reads them.
//
// Use it on any page (the Desk, Kevin's portal, Fiona's board, James's page):
//   <script src="/markup.js"></script>
//   PdfMarkup.mount(document.getElementById('box'), { id: 'apex-admin-87', key: 'picture', headers: { 'X-Desk': '1' } });
// Options:
//   id, key     the ticket and its attachment
//   base        prefix of the server routes ('' on the Desk); the server offers:
//                 GET  <base>/api/markup/info?id=&key=          -> { pages: [{w, h}], image, saveAs, version, label, title, user }
//                 GET  <base>/api/markup/page?id=&key=&n=&zoom= -> PNG of page n (0-based) at zoom (1 = 72 dpi)
//                 POST <base>/api/markup/save {id, key, annots} -> { ok, count, key }  (key: a picture's new marked-up copy)
//   headers     extra request headers (the Desk needs X-Desk: 1 on POST; a portal adds its own sign-in)
//   onSaved(result)   called after a save
// Annotations are sent in page points, top-left origin, y down, as the page is displayed:
//   { type: 'box'|'highlight'|'text', page, rect: [x0, y0, x1, y1], text? }  or  { type: 'arrow', page, from: [x, y], to: [x, y] }
(function () {
  const RED = '#d91a1a', SVG = 'http://www.w3.org/2000/svg';
  const css = `
.pmk { font: 14px system-ui, Segoe UI, sans-serif; color: var(--ink, #1c2024); }
.pmk-bar { position: sticky; top: 0; z-index: 5; display: flex; gap: 6px; flex-wrap: wrap; align-items: center; padding: 8px; background: var(--panel, #fff); border-bottom: 1px solid var(--line, #e3e6ea); }
.pmk-bar button { font: inherit; padding: 6px 12px; border-radius: 6px; border: 1px solid var(--line, #cfd4da); background: var(--panel, #fff); color: inherit; cursor: pointer; }
.pmk-bar button.on { border-color: ${RED}; box-shadow: inset 0 0 0 1px ${RED}; color: ${RED}; }
.pmk-bar button.save { background: #2563eb; border-color: #2563eb; color: #fff; }
.pmk-bar button:disabled { opacity: .5; cursor: default; }
.pmk-msg { margin-left: 8px; color: var(--muted, #646d78); font-size: 13px; }
.pmk-msg.bad { color: ${RED}; }
.pmk-pages { display: grid; gap: 12px; padding: 12px; justify-items: center; }
.pmk-page { position: relative; box-shadow: 0 1px 4px rgba(0,0,0,.25); background: #fff; touch-action: none; }
.pmk-page img { display: block; width: 100%; height: 100%; user-select: none; -webkit-user-drag: none; }
.pmk-page svg { position: absolute; inset: 0; width: 100%; height: 100%; cursor: crosshair; }
.pmk-page textarea { position: absolute; font: 13px Helvetica, Arial, sans-serif; color: ${RED}; border: 1px solid ${RED}; background: rgba(255,255,255,.9); resize: none; padding: 2px 4px; box-sizing: border-box; }
.pmk-pageno { position: absolute; right: 6px; bottom: 4px; font-size: 11px; color: #888; pointer-events: none; }`;

  function mount(root, opt) {
    if (!document.getElementById('pmk-css')) { const s = document.createElement('style'); s.id = 'pmk-css'; s.textContent = css; document.head.appendChild(s); }
    const base = opt.base || '';
    const headers = Object.assign({ 'Content-Type': 'application/json' }, opt.headers || {});
    let info = null, tool = 'box', pending = [], key = opt.key, busy = false, typing = false;
    const q = () => `id=${encodeURIComponent(opt.id)}&key=${encodeURIComponent(key)}`; // after a picture's first save: its marked-up copy
    root.classList.add('pmk');
    root.innerHTML = `<div class="pmk-bar">
      <button data-t="highlight" title="Drag over what to highlight">Highlight</button>
      <button data-t="box" title="Drag a box">Box</button>
      <button data-t="arrow" title="Drag from the tail to the point">Arrow</button>
      <button data-t="text" title="Drag where the text goes (or click), then type">Text</button>
      <button data-a="undo" title="Undo the last unsaved mark-up (Ctrl+Z)">Undo</button>
      <button data-a="save" class="save" title="Write the mark-ups into the file">Save</button>
      <span class="pmk-msg"></span></div><div class="pmk-pages"></div>`;
    const bar = root.querySelector('.pmk-bar'), pagesEl = root.querySelector('.pmk-pages'), msg = root.querySelector('.pmk-msg');
    const say = (t, bad) => { msg.textContent = t; msg.classList.toggle('bad', !!bad); };
    const sync = () => {
      bar.querySelectorAll('[data-t]').forEach(b => b.classList.toggle('on', b.dataset.t === tool));
      bar.querySelector('[data-a="undo"]').disabled = !pending.length || busy;
      bar.querySelector('[data-a="save"]').disabled = !pending.length || busy;
      if (!busy) say(pending.length ? `${pending.length} unsaved mark-up${pending.length === 1 ? '' : 's'}` : (info ? 'Pick a tool and draw on the page.' : ''));
    };
    bar.onclick = e => {
      const b = e.target.closest('button'); if (!b) return;
      if (b.dataset.t) { tool = b.dataset.t; sync(); }
      else if (b.dataset.a === 'undo') undo();
      else if (b.dataset.a === 'save') save();
    };
    const onKey = e => { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z' && !(e.target.matches && e.target.matches('textarea, input'))) { e.preventDefault(); undo(); } };
    document.addEventListener('keydown', onKey);

    async function load() {
      say('Opening…');
      const r = await fetch(`${base}/api/markup/info?${q()}`, { headers: opt.headers || {} });
      const j = await r.json().catch(() => ({}));
      if (!r.ok) { say(j.error || 'It couldn\'t be opened.', true); return; }
      info = j; pending = []; draw(); sync();
      if (opt.onInfo) opt.onInfo(info);
    }

    function draw() {
      pagesEl.innerHTML = '';
      const avail = Math.max(320, (pagesEl.clientWidth || root.clientWidth || 900) - 24);
      info.pages.forEach((p, n) => {
        const w = Math.min(avail, 1600), scale = w / p.w, h = p.h * scale;
        const zoom = Math.min(4, Math.max(0.5, Math.ceil(scale * (window.devicePixelRatio || 1) * 2) / 2));
        const el = document.createElement('div');
        el.className = 'pmk-page'; el.style.width = w + 'px'; el.style.height = h + 'px';
        el.innerHTML = `<img alt="Page ${n + 1}" src="${base}/api/markup/page?${q()}&n=${n}&zoom=${zoom}&v=${encodeURIComponent(info.version)}"><svg viewBox="0 0 ${p.w} ${p.h}" preserveAspectRatio="none">
          <defs><marker id="pmk-ah${n}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10" fill="none" stroke="${RED}" stroke-width="2"/></marker></defs></svg>
          <span class="pmk-pageno">${n + 1} / ${info.pages.length}</span>`;
        pagesEl.appendChild(el);
        wirePage(el, n, p);
      });
    }

    // Points from a pointer event, in page points.
    const pt = (svg, p, e) => { const r = svg.getBoundingClientRect(); return [(e.clientX - r.left) * p.w / r.width, (e.clientY - r.top) * p.h / r.height]; };
    function shape(a) {
      const s = a.type === 'arrow' ? document.createElementNS(SVG, 'line') : document.createElementNS(SVG, 'rect');
      if (a.type === 'arrow') {
        s.setAttribute('x1', a.from[0]); s.setAttribute('y1', a.from[1]); s.setAttribute('x2', a.to[0]); s.setAttribute('y2', a.to[1]);
        s.setAttribute('stroke', RED); s.setAttribute('stroke-width', 1.5); s.setAttribute('marker-end', `url(#pmk-ah${a.page})`);
      } else {
        const [x0, y0, x1, y1] = a.rect;
        s.setAttribute('x', Math.min(x0, x1)); s.setAttribute('y', Math.min(y0, y1)); s.setAttribute('width', Math.abs(x1 - x0)); s.setAttribute('height', Math.abs(y1 - y0));
        if (a.type === 'highlight') { s.setAttribute('fill', 'rgba(255,217,51,.45)'); }
        else { s.setAttribute('fill', 'none'); s.setAttribute('stroke', RED); s.setAttribute('stroke-width', a.type === 'text' ? 1 : 1.5); }
      }
      s.setAttribute('vector-effect', 'non-scaling-stroke');
      return s;
    }
    function label(svg, a) {
      if (a.type !== 'text' || !a.text) return null;
      const t = document.createElementNS(SVG, 'text');
      t.setAttribute('x', Math.min(a.rect[0], a.rect[2]) + 3); t.setAttribute('y', Math.min(a.rect[1], a.rect[3]) + 13);
      t.setAttribute('fill', RED); t.setAttribute('font-size', 11); t.setAttribute('font-family', 'Helvetica, Arial, sans-serif');
      t.textContent = a.text.length > 120 ? a.text.slice(0, 120) + '…' : a.text;
      svg.appendChild(t); return t;
    }

    function wirePage(el, n, p) {
      const svg = el.querySelector('svg');
      let start = null, live = null;
      svg.addEventListener('pointerdown', e => {
        if (busy || typing || e.button !== 0) return; // a click away from a text box only finishes that box
        e.preventDefault(); svg.setPointerCapture(e.pointerId);
        start = pt(svg, p, e);
        live = shape(tool === 'arrow' ? { type: 'arrow', page: n, from: start, to: start } : { type: tool, page: n, rect: [...start, ...start] });
        svg.appendChild(live);
      });
      svg.addEventListener('pointermove', e => {
        if (!start) return;
        const c = pt(svg, p, e);
        const s = shape(tool === 'arrow' ? { type: 'arrow', page: n, from: start, to: c } : { type: tool, page: n, rect: [...start, ...c] });
        live.replaceWith(s); live = s;
      });
      svg.addEventListener('pointerup', e => {
        if (!start) return;
        const c = pt(svg, p, e), s0 = start; start = null;
        live.remove(); live = null;
        if (tool === 'arrow') { if (Math.hypot(c[0] - s0[0], c[1] - s0[1]) > 4) add(svg, { type: 'arrow', page: n, from: s0, to: c }); return; }
        let rect = [Math.min(s0[0], c[0]), Math.min(s0[1], c[1]), Math.max(s0[0], c[0]), Math.max(s0[1], c[1])];
        if (tool === 'text') {
          if (rect[2] - rect[0] < 20 || rect[3] - rect[1] < 12) rect = [s0[0], s0[1], Math.min(p.w, s0[0] + 180), Math.min(p.h, s0[1] + 40)];
          askText(el, svg, p, n, rect);
          return;
        }
        if (rect[2] - rect[0] > 3 && rect[3] - rect[1] > 3) add(svg, { type: tool, page: n, rect });
      });
    }

    function askText(el, svg, p, n, rect) {
      const ta = document.createElement('textarea'), k = el.clientWidth / p.w;
      ta.style.left = rect[0] * k + 'px'; ta.style.top = rect[1] * k + 'px';
      ta.style.width = Math.max(120, (rect[2] - rect[0]) * k) + 'px'; ta.style.height = Math.max(36, (rect[3] - rect[1]) * k) + 'px';
      ta.placeholder = 'Type, then click away (Esc cancels)';
      el.appendChild(ta); ta.focus(); typing = true;
      let done = false;
      const finish = keep => {
        if (done) return; done = true; setTimeout(() => { typing = false; }, 0);
        const text = ta.value.trim(); ta.remove();
        if (keep && text) add(svg, { type: 'text', page: n, rect, text });
      };
      ta.addEventListener('blur', () => finish(true));
      ta.addEventListener('keydown', e => { if (e.key === 'Escape') finish(false); if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) finish(true); });
    }

    function add(svg, a) {
      const s = shape(a); svg.appendChild(s);
      a._els = [s, label(svg, a)].filter(Boolean);
      pending.push(a); sync();
    }
    function undo() {
      if (busy) return;
      const a = pending.pop(); if (!a) return;
      a._els.forEach(x => x.remove()); sync();
    }
    async function save() {
      if (!pending.length || busy) return;
      busy = true; sync(); say('Saving…');
      try {
        const annots = pending.map(({ _els, ...a }) => a);
        const r = await fetch(`${base}/api/markup/save`, { method: 'POST', headers, body: JSON.stringify({ id: opt.id, key, annots }) });
        const j = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(j.error || 'The save didn\'t work.');
        busy = false;
        if (j.key && j.key !== key) { key = j.key; } // a picture: later rounds add to its marked-up copy
        const r2 = await fetch(`${base}/api/markup/info?${q()}`, { headers: opt.headers || {} });
        if (r2.ok) info = await r2.json();
        pending = []; draw(); sync();
        say(`Saved ${j.count} mark-up${j.count === 1 ? '' : 's'} into ${info.label || 'the file'}.`);
        if (opt.onSaved) opt.onSaved(Object.assign({ key }, j));
      } catch (e) { busy = false; sync(); say(e.message, true); }
    }

    let resizeTimer = 0;
    const onResize = () => { clearTimeout(resizeTimer); resizeTimer = setTimeout(() => { if (info && !pending.length) draw(); }, 250); };
    window.addEventListener('resize', onResize);
    load();
    return {
      unsaved: () => pending.length,
      destroy() { document.removeEventListener('keydown', onKey); window.removeEventListener('resize', onResize); root.innerHTML = ''; }
    };
  }
  window.PdfMarkup = { mount };
})();
