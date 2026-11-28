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
//   { type: 'box'|'cloud'|'highlight'|'text', page, rect: [x0, y0, x1, y1], text? },  { type: 'arrow', page, from: [x, y], to: [x, y] }
//   or { type: 'callout', page, rect, point: [x, y] (where its arrow points), text }; each with color: '#rrggbb' (the swatch picked)
(function () {
  const RED = '#d91a1a', SVG = 'http://www.w3.org/2000/svg';
  // Grant's swatches (28 Nov): red first (the default), then blue, green, magenta, black. The pick is kept per browser.
  const COLOURS = [['#d91a1a', 'Red'], ['#1f5fd6', 'Blue'], ['#1a8f3c', 'Green'], ['#c21bb5', 'Magenta'], ['#111111', 'Black']];
  const ci = c => Math.max(0, COLOURS.findIndex(x => x[0] === c));
  const translucent = (c, a) => `rgba(${parseInt(c.slice(1, 3), 16)},${parseInt(c.slice(3, 5), 16)},${parseInt(c.slice(5, 7), 16)},${a})`;
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
.pmk-sw { display: inline-flex; gap: 4px; margin: 0 4px; }
.pmk-sw button { width: 22px; height: 22px; padding: 0; border-radius: 50%; border: 2px solid var(--panel, #fff); box-shadow: 0 0 0 1px var(--line, #cfd4da); }
.pmk-sw button.on { box-shadow: 0 0 0 2px var(--ink, #1c2024); }
.pmk-pageno { position: absolute; right: 6px; bottom: 4px; font-size: 11px; color: #888; pointer-events: none; }`;

  function mount(root, opt) {
    if (!document.getElementById('pmk-css')) { const s = document.createElement('style'); s.id = 'pmk-css'; s.textContent = css; document.head.appendChild(s); }
    const base = opt.base || '';
    const headers = Object.assign({ 'Content-Type': 'application/json' }, opt.headers || {});
    let info = null, tool = 'box', pending = [], key = opt.key, busy = false, typing = false;
    let colour = (() => { try { const c = localStorage.getItem('pmk-colour'); return COLOURS.some(x => x[0] === c) ? c : RED; } catch { return RED; } })();
    const q = () => `id=${encodeURIComponent(opt.id)}&key=${encodeURIComponent(key)}`; // after a picture's first save: its marked-up copy
    root.classList.add('pmk');
    root.innerHTML = `<div class="pmk-bar">
      <button data-t="highlight" title="Drag over what to highlight">Highlight</button>
      <button data-t="box" title="Drag a box">Box</button>
      <button data-t="arrow" title="Drag from the tail to the point">Arrow</button>
      <button data-t="cloud" title="Drag a revision cloud round an area">Cloud</button>
      <button data-t="text" title="Drag where the text goes (or click), then type">Text</button>
      <button data-t="callout" title="Drag from the point to where the text goes, then type">Callout</button>
      <span class="pmk-sw">${COLOURS.map(([c, n]) => `<button data-c="${c}" title="${n}" aria-label="${n}" style="background:${c}"></button>`).join('')}</span>
      <button data-a="undo" title="Undo the last unsaved mark-up (Ctrl+Z)">Undo</button>
      <button data-a="save" class="save" title="Write the mark-ups into the file">Save</button>
      <span class="pmk-msg"></span></div><div class="pmk-pages"></div>`;
    const bar = root.querySelector('.pmk-bar'), pagesEl = root.querySelector('.pmk-pages'), msg = root.querySelector('.pmk-msg');
    const say = (t, bad) => { msg.textContent = t; msg.classList.toggle('bad', !!bad); };
    const sync = () => {
      bar.querySelectorAll('[data-t]').forEach(b => b.classList.toggle('on', b.dataset.t === tool));
      bar.querySelectorAll('[data-c]').forEach(b => b.classList.toggle('on', b.dataset.c === colour));
      bar.querySelector('[data-a="undo"]').disabled = !pending.length || busy;
      bar.querySelector('[data-a="save"]').disabled = !pending.length || busy;
      if (!busy) say(pending.length ? `${pending.length} unsaved mark-up${pending.length === 1 ? '' : 's'}` : (info ? 'Pick a tool and draw on the page.' : ''));
    };
    bar.onclick = e => {
      const b = e.target.closest('button'); if (!b) return;
      if (b.dataset.t) { tool = b.dataset.t; sync(); }
      else if (b.dataset.c) { colour = b.dataset.c; try { localStorage.setItem('pmk-colour', colour); } catch { } sync(); }
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
      const avail = Math.max(320, (pagesEl.clientWidth || root.clientWidth || 900) - 44); // padding and a scroll bar
      info.pages.forEach((p, n) => {
        const w = Math.min(avail, 1600), scale = w / p.w, h = p.h * scale;
        const zoom = Math.min(4, Math.max(0.5, Math.ceil(scale * (window.devicePixelRatio || 1) * 2) / 2));
        const el = document.createElement('div');
        el.className = 'pmk-page'; el.style.width = w + 'px'; el.style.height = h + 'px';
        el.innerHTML = `<img alt="Page ${n + 1}" src="${base}/api/markup/page?${q()}&n=${n}&zoom=${zoom}&v=${encodeURIComponent(info.version)}"><svg viewBox="0 0 ${p.w} ${p.h}" preserveAspectRatio="none">
          <defs>${COLOURS.map(([c], i) => `<marker id="pmk-ah${n}-${i}" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse"><path d="M0,0 L10,5 L0,10" fill="none" stroke="${c}" stroke-width="2"/></marker>`).join('')}</defs></svg>
          <span class="pmk-pageno">${n + 1} / ${info.pages.length}</span>`;
        pagesEl.appendChild(el);
        wirePage(el, n, p);
      });
    }

    // Points from a pointer event, in page points.
    const pt = (svg, p, e) => { const r = svg.getBoundingClientRect(); return [(e.clientX - r.left) * p.w / r.width, (e.clientY - r.top) * p.h / r.height]; };
    // A revision cloud's outline: scallops along each side of the rectangle.
    function cloudD(x0, y0, x1, y1) {
      const [l, t, r, b] = [Math.min(x0, x1), Math.min(y0, y1), Math.max(x0, x1), Math.max(y0, y1)];
      const step = Math.max(8, Math.min(18, Math.min(r - l, b - t) / 3 || 8)), pts = [];
      const side = (ax, ay, bx, by) => { const n = Math.max(1, Math.round(Math.hypot(bx - ax, by - ay) / step)); for (let i = 0; i < n; i++) pts.push([ax + (bx - ax) * i / n, ay + (by - ay) * i / n]); };
      side(l, t, r, t); side(r, t, r, b); side(r, b, l, b); side(l, b, l, t);
      pts.push(pts[0]);
      let d = `M${pts[0][0]},${pts[0][1]}`;
      for (let i = 1; i < pts.length; i++) { const rr = Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]) / 2; d += ` A${rr},${rr} 0 0 1 ${pts[i][0]},${pts[i][1]}`; }
      return d;
    }
    // Where a callout's leader line meets its box: the nearest side's middle or corner.
    const knee = (r, p) => [p[0] < r[0] ? r[0] : p[0] > r[2] ? r[2] : (r[0] + r[2]) / 2, p[1] < r[1] ? r[1] : p[1] > r[3] ? r[3] : (r[1] + r[3]) / 2];
    function shape(a) {
      const c = a.color || RED, ah = `url(#pmk-ah${a.page}-${ci(c)})`;
      if (a.type === 'cloud') {
        const s = document.createElementNS(SVG, 'path');
        s.setAttribute('d', cloudD(...a.rect)); s.setAttribute('fill', 'none'); s.setAttribute('stroke', c); s.setAttribute('stroke-width', 1.5);
        s.setAttribute('vector-effect', 'non-scaling-stroke'); return s;
      }
      if (a.type === 'callout') {
        const g = document.createElementNS(SVG, 'g'), [x0, y0, x1, y1] = a.rect, k = knee(a.rect, a.point);
        g.innerHTML = `<rect x="${x0}" y="${y0}" width="${x1 - x0}" height="${y1 - y0}" fill="rgba(255,255,255,.85)" stroke="${c}" stroke-width="1" vector-effect="non-scaling-stroke"/>`
          + `<line x1="${k[0]}" y1="${k[1]}" x2="${a.point[0]}" y2="${a.point[1]}" stroke="${c}" stroke-width="1.5" marker-end="${ah}" vector-effect="non-scaling-stroke"/>`;
        return g;
      }
      const s = a.type === 'arrow' ? document.createElementNS(SVG, 'line') : document.createElementNS(SVG, 'rect');
      if (a.type === 'arrow') {
        s.setAttribute('x1', a.from[0]); s.setAttribute('y1', a.from[1]); s.setAttribute('x2', a.to[0]); s.setAttribute('y2', a.to[1]);
        s.setAttribute('stroke', c); s.setAttribute('stroke-width', 1.5); s.setAttribute('marker-end', ah);
      } else {
        const [x0, y0, x1, y1] = a.rect;
        s.setAttribute('x', Math.min(x0, x1)); s.setAttribute('y', Math.min(y0, y1)); s.setAttribute('width', Math.abs(x1 - x0)); s.setAttribute('height', Math.abs(y1 - y0));
        if (a.type === 'highlight') { s.setAttribute('fill', translucent(c, .35)); }
        else { s.setAttribute('fill', 'none'); s.setAttribute('stroke', c); s.setAttribute('stroke-width', a.type === 'text' ? 1 : 1.5); }
      }
      s.setAttribute('vector-effect', 'non-scaling-stroke');
      return s;
    }
    function label(svg, a) {
      if (!['text', 'callout'].includes(a.type) || !a.text) return null;
      const t = document.createElementNS(SVG, 'text');
      t.setAttribute('x', Math.min(a.rect[0], a.rect[2]) + 3); t.setAttribute('y', Math.min(a.rect[1], a.rect[3]) + 13);
      t.setAttribute('fill', a.color || RED); t.setAttribute('font-size', 11); t.setAttribute('font-family', 'Helvetica, Arial, sans-serif');
      t.textContent = a.text.length > 120 ? a.text.slice(0, 120) + '…' : a.text;
      svg.appendChild(t); return t;
    }

    // While dragging: what the drawn shape will be. A callout drags from the point it marks to where its text goes.
    const calloutBox = (p, c) => { const x = Math.min(Math.max(0, c[0]), p.w - 180), y = Math.min(Math.max(0, c[1] - 20), p.h - 40); return [x, y, x + 180, y + 40]; };
    function preview(n, s0, c) {
      const pg = info.pages[n];
      if (tool === 'arrow') return { type: 'arrow', page: n, from: s0, to: c, color: colour };
      if (tool === 'callout') return { type: 'callout', page: n, point: s0, rect: calloutBox(pg, c), color: colour };
      return { type: tool, page: n, rect: [...s0, ...c], color: colour };
    }
    function wirePage(el, n, p) {
      const svg = el.querySelector('svg');
      let start = null, live = null;
      svg.addEventListener('pointerdown', e => {
        if (busy || typing || e.button !== 0) return; // a click away from a text box only finishes that box
        e.preventDefault(); svg.setPointerCapture(e.pointerId);
        start = pt(svg, p, e);
        live = shape(preview(n, start, start));
        svg.appendChild(live);
      });
      svg.addEventListener('pointermove', e => {
        if (!start) return;
        const c = pt(svg, p, e);
        const s = shape(preview(n, start, c));
        live.replaceWith(s); live = s;
      });
      svg.addEventListener('pointerup', e => {
        if (!start) return;
        const c = pt(svg, p, e), s0 = start; start = null;
        live.remove(); live = null;
        if (tool === 'arrow') { if (Math.hypot(c[0] - s0[0], c[1] - s0[1]) > 4) add(svg, { type: 'arrow', page: n, from: s0, to: c }); return; }
        if (tool === 'callout') { askText(el, svg, p, n, calloutBox(p, c), s0); return; }
        let rect = [Math.min(s0[0], c[0]), Math.min(s0[1], c[1]), Math.max(s0[0], c[0]), Math.max(s0[1], c[1])];
        if (tool === 'text') {
          if (rect[2] - rect[0] < 20 || rect[3] - rect[1] < 12) rect = [s0[0], s0[1], Math.min(p.w, s0[0] + 180), Math.min(p.h, s0[1] + 40)];
          askText(el, svg, p, n, rect);
          return;
        }
        if (rect[2] - rect[0] > 3 && rect[3] - rect[1] > 3) add(svg, { type: tool, page: n, rect });
      });
    }

    function askText(el, svg, p, n, rect, point) {
      const ta = document.createElement('textarea'), k = el.clientWidth / p.w;
      ta.style.left = rect[0] * k + 'px'; ta.style.top = rect[1] * k + 'px';
      ta.style.width = Math.max(120, (rect[2] - rect[0]) * k) + 'px'; ta.style.height = Math.max(36, (rect[3] - rect[1]) * k) + 'px';
      ta.placeholder = 'Type, then click away (Esc cancels)';
      ta.style.color = ta.style.borderColor = colour;
      el.appendChild(ta); ta.focus(); typing = true;
      let done = false;
      const finish = keep => {
        if (done) return; done = true; setTimeout(() => { typing = false; }, 0);
        const text = ta.value.trim(); ta.remove();
        if (keep && text) add(svg, point ? { type: 'callout', page: n, rect, point, text } : { type: 'text', page: n, rect, text });
      };
      ta.addEventListener('blur', () => finish(true));
      ta.addEventListener('keydown', e => { if (e.key === 'Escape') finish(false); if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) finish(true); });
    }

    function add(svg, a) {
      a.color = a.color || colour;
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
