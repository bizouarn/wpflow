// Pont JS de Wpflow.Blazor : mesure des nodes et des handles, molette, suivi du pointeur pendant un geste,
// pincement tactile, touches du navigateur à neutraliser, export d'image. Tout le reste (état, gestes, rendu) est en C#.

const roots = new WeakMap();

// Contrôles HTML d'un node : un appui dessus ne démarre pas de geste (le node est seulement sélectionné).
const INTERACTIVE = 'input,textarea,select,button,a,[contenteditable="true"]';
const TEXT_INPUT = 'input,textarea,select,[contenteditable="true"]';

export function attach(root, dotnet) {
    const s = {
        root, dotnet,
        nodes: new Map(),      // id -> élément
        pending: new Set(),    // ids à remesurer
        raf: 0,
        rect: null,
        pointerId: null,
        moveRaf: 0,
        lastMove: null,
        pointers: new Map(),   // pointeurs enfoncés (pincement)
        pinch: null,
        options: { preventScrolling: true, pinch: true, zoomOnScroll: true, panOnScroll: false }
    };
    roots.set(root, s);

    const sendRect = (force) => {
        const r = root.getBoundingClientRect();
        const next = [r.left, r.top, r.width, r.height];
        if (!force && s.rect && next.every((v, i) => Math.abs(v - s.rect[i]) < 0.5)) return;
        s.rect = next;
        dotnet.invokeMethodAsync('OnRootRect', ...next);
    };
    s.sendRect = sendRect;

    s.ro = new ResizeObserver(entries => {
        for (const e of entries) {
            if (e.target === root) sendRect(false);
            else {
                const id = e.target.dataset.nodeId;
                if (id) queue(s, id);
            }
        }
    });
    s.ro.observe(root);

    s.onScroll = () => sendRect(false);
    window.addEventListener('scroll', s.onScroll, true);
    window.addEventListener('resize', s.onScroll);

    // Molette : écoutée ici car preventDefault doit être appelé par un écouteur non passif.
    s.onWheel = (e) => {
        if (e.target.closest && e.target.closest('.nowheel')) return;
        const zoomKey = e.ctrlKey || e.metaKey;
        const handled = s.options.zoomOnScroll || s.options.panOnScroll || zoomKey;
        if (!handled && !s.options.preventScrolling) return; // la page défile
        e.preventDefault();
        sendRect(false);
        dotnet.invokeMethodAsync('OnWheel', e.clientX, e.clientY, e.deltaX, e.deltaY, e.ctrlKey, e.shiftKey, e.metaKey, e.deltaMode);
    };
    root.addEventListener('wheel', s.onWheel, { passive: false });

    // Début de geste : tourne avant le dispatcher d'évènements de Blazor (plus haut dans l'arbre).
    s.onDown = (e) => {
        s.pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (s.pointers.size >= 2 && s.options.pinch) {
            // Un second doigt : le geste en cours est annulé, on passe en pincement.
            e.stopPropagation();
            if (s.pointerId !== null) {
                const id = s.pointerId;
                s.pointerId = null;
                try { root.releasePointerCapture(id); } catch { /* déjà relâché */ }
                dotnet.invokeMethodAsync('OnPointerUp', e.clientX, e.clientY, false, true);
            }
            startPinch(s);
            return;
        }

        if (e.button !== 0 && e.button !== 1 && e.button !== 2) return;
        const t = e.target;
        const closest = (sel) => (t.closest ? t.closest(sel) : null);
        let flags = 0;
        if (closest('.nodrag')) flags |= 1;
        const nodeEl = closest('.wf-node');
        if (nodeEl && nodeEl.dataset.dragHandle) {
            const h = nodeEl.dataset.dragHandle;
            const sel = /^[.#\[]/.test(h) ? h : '.' + h;
            let inside = false;
            try { inside = !!closest(sel) && nodeEl.contains(closest(sel)); } catch { /* sélecteur invalide */ }
            if (!inside) flags |= 1;
        }
        if (closest('.nopan')) flags |= 2;
        if (closest(INTERACTIVE)) flags |= 4;
        if (flags) dotnet.invokeMethodAsync('OnPressInfo', flags);
        if (flags & 4) return; // contrôle HTML : pas de capture, il garde son comportement

        if (s.pointerId !== null) return;
        sendRect(false);
        s.pointerId = e.pointerId;
        try { root.setPointerCapture(e.pointerId); } catch { /* pointeur déjà relâché */ }
        if (e.button === 1) e.preventDefault();
    };
    s.onMove = (e) => {
        const p = s.pointers.get(e.pointerId);
        if (p) { p.x = e.clientX; p.y = e.clientY; }
        if (s.pinch) { movePinch(s); return; }
        if (e.pointerId !== s.pointerId) return;
        s.lastMove = e;
        if (s.moveRaf) return;
        s.moveRaf = requestAnimationFrame(() => {
            s.moveRaf = 0;
            const m = s.lastMove;
            if (m && s.pointerId !== null) dotnet.invokeMethodAsync('OnPointerMove', m.clientX, m.clientY, m.shiftKey, m.ctrlKey || m.metaKey);
        });
    };
    s.onUp = (e) => {
        s.pointers.delete(e.pointerId);
        if (s.pinch && s.pointers.size < 2) s.pinch = null;
        if (e.pointerId !== s.pointerId) return;
        s.pointerId = null;
        if (s.moveRaf) { cancelAnimationFrame(s.moveRaf); s.moveRaf = 0; }
        try { root.releasePointerCapture(e.pointerId); } catch { /* ignoré */ }
        dotnet.invokeMethodAsync('OnPointerUp', e.clientX, e.clientY, e.shiftKey, e.type === 'pointercancel');
    };
    root.addEventListener('pointerdown', s.onDown);
    root.addEventListener('pointermove', s.onMove);
    root.addEventListener('pointerup', s.onUp);
    root.addEventListener('pointercancel', s.onUp);

    // Clavier : les saisies dans un champ ne sont pas vues par le graphe ; les touches du graphe n'agissent pas sur la page.
    s.onKey = (e) => {
        if (e.target.closest && e.target.closest(TEXT_INPUT)) { e.stopPropagation(); return; }
        const k = e.key;
        const ctrl = e.ctrlKey || e.metaKey;
        if (k === ' ' || k.startsWith('Arrow') || (ctrl && ['a', 'z', 'y'].includes(k.toLowerCase()))) e.preventDefault();
    };
    root.addEventListener('keydown', s.onKey);

    sendRect(true);
}

export function setOptions(root, options) {
    const s = roots.get(root);
    if (s) Object.assign(s.options, options);
}

export function detach(root) {
    const s = roots.get(root);
    if (!s) return;
    s.ro.disconnect();
    window.removeEventListener('scroll', s.onScroll, true);
    window.removeEventListener('resize', s.onScroll);
    root.removeEventListener('wheel', s.onWheel);
    root.removeEventListener('pointerdown', s.onDown);
    root.removeEventListener('pointermove', s.onMove);
    root.removeEventListener('pointerup', s.onUp);
    root.removeEventListener('pointercancel', s.onUp);
    root.removeEventListener('keydown', s.onKey);
    if (s.raf) cancelAnimationFrame(s.raf);
    roots.delete(root);
}

function pinchState(s) {
    const [a, b] = [...s.pointers.values()];
    return { dist: Math.hypot(a.x - b.x, a.y - b.y), cx: (a.x + b.x) / 2, cy: (a.y + b.y) / 2 };
}

function startPinch(s) {
    if (s.pointers.size >= 2) s.pinch = pinchState(s);
}

function movePinch(s) {
    if (s.pointers.size < 2) return;
    const cur = pinchState(s);
    const prev = s.pinch;
    s.pinch = cur;
    if (!prev || prev.dist === 0) return;
    s.dotnet.invokeMethodAsync('OnPinch', cur.cx, cur.cy, cur.dist / prev.dist, cur.cx - prev.cx, cur.cy - prev.cy);
}

export function observeNode(root, id) {
    const s = roots.get(root);
    if (!s) return;
    const el = root.querySelector('[data-node-id="' + CSS.escape(id) + '"]');
    if (!el || s.nodes.get(id) === el) return;
    const old = s.nodes.get(id);
    if (old) s.ro.unobserve(old);
    s.nodes.set(id, el);
    s.ro.observe(el);
    queue(s, id);
}

export function unobserveNode(root, id) {
    const s = roots.get(root);
    if (!s) return;
    const el = s.nodes.get(id);
    if (el) s.ro.unobserve(el);
    s.nodes.delete(id);
}

/** Force une nouvelle mesure (handles ajoutés ou déplacés sans changement de taille du node). */
export function remeasure(root, id) {
    const s = roots.get(root);
    if (s) queue(s, id);
}

function queue(s, id) {
    s.pending.add(id);
    if (s.raf) return;
    s.raf = requestAnimationFrame(() => {
        s.raf = 0;
        const batch = [];
        for (const nid of s.pending) {
            const el = s.nodes.get(nid);
            if (el && el.isConnected) {
                const m = measure(el, nid);
                if (m) batch.push(m);
            }
        }
        s.pending.clear();
        if (batch.length) s.dotnet.invokeMethodAsync('OnMeasured', batch);
    });
}

function measure(el, id) {
    const w = el.offsetWidth, h = el.offsetHeight;
    if (!w || !h) return null;
    const nr = el.getBoundingClientRect();
    // Zoom du viewport (offsetWidth ignore les transformations) ; une rotation élargit le rectangle : on ne s'en sert que pour l'échelle.
    const scale = el.getBoundingClientRect().width / w || 1;
    const rotated = el.style.transform.includes('rotate');
    const k = rotated ? (el.closest('.wf-viewport').getBoundingClientRect().width / (el.closest('.wf-viewport').offsetWidth || 1) || 1) : scale;
    const handles = [];
    for (const he of el.querySelectorAll('.wf-handle')) {
        const r = he.getBoundingClientRect();
        handles.push({
            id: he.dataset.handleid || null,
            type: he.dataset.type,
            position: he.dataset.position,
            x: (r.left - nr.left) / k,
            y: (r.top - nr.top) / k,
            width: r.width / k,
            height: r.height / k,
            connectable: he.dataset.connectable !== 'false',
            connectableStart: he.dataset.connectableStart !== 'false',
            connectableEnd: he.dataset.connectableEnd !== 'false'
        });
    }
    return { id, width: w, height: h, handles };
}

/** Télécharge une image : `.svg` tel quel, sinon rastérisée (PNG) à l'échelle demandée. */
export async function saveImage(svg, filename, scale) {
    let blob;
    if (filename.toLowerCase().endsWith('.svg')) {
        blob = new Blob([svg], { type: 'image/svg+xml' });
    } else {
        const url = URL.createObjectURL(new Blob([svg], { type: 'image/svg+xml' }));
        const img = new Image();
        await new Promise((resolve, reject) => { img.onload = resolve; img.onerror = reject; img.src = url; });
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(img.width * scale));
        canvas.height = Math.max(1, Math.round(img.height * scale));
        canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
        URL.revokeObjectURL(url);
        blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    }
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}

/** Coin haut gauche et taille d un element (pixels page). */
export function rectOf(el) {
    const r = el.getBoundingClientRect();
    return [r.left, r.top, r.width, r.height];
}

/**
 * Capture le rendu réel du graphe (nodes à contenu personnalisé, styles, thème) : le calque est cloné avec ses styles calculés,
 * placé dans un SVG (foreignObject) puis rastérisé (PNG) ou enregistré tel quel (SVG). La zone (x, y, w, h) est en coordonnées flow.
 */
export async function saveImageFromDom(root, x, y, w, h, filename, scale) {
    const viewport = root.querySelector('.wf-viewport');
    const clone = viewport.cloneNode(true);
    const originals = [viewport, ...viewport.querySelectorAll('*')];
    const copies = [clone, ...clone.querySelectorAll('*')];
    originals.forEach((el, i) => {
        const cs = getComputedStyle(el);
        let css = '';
        for (const p of cs) css += p + ':' + cs.getPropertyValue(p) + ';';
        copies[i].setAttribute('style', css);
    });
    // Poignées, barres d'outils et ancres de reconnexion sont des aides d'édition : pas dans l'image.
    for (const el of clone.querySelectorAll('.wf-resizer,.wf-rotator,.wf-toolbar,.wf-selection-toolbar,.wf-edge-anchor,.wf-selection-box')) el.remove();
    clone.style.transform = 'translate(' + (-x) + 'px,' + (-y) + 'px)';

    const background = getComputedStyle(root).backgroundColor;
    const wrapper = document.createElement('div');
    wrapper.setAttribute('style', 'position:relative;overflow:hidden;width:' + w + 'px;height:' + h + 'px;background:' + background);
    wrapper.appendChild(clone);
    const xhtml = new XMLSerializer().serializeToString(wrapper);
    const svg = '<svg xmlns="http://www.w3.org/2000/svg" width="' + w + '" height="' + h + '"><foreignObject width="100%" height="100%">' + xhtml + '</foreignObject></svg>';

    let blob;
    if (filename.toLowerCase().endsWith('.svg')) {
        blob = new Blob([svg], { type: 'image/svg+xml' });
    } else {
        const img = new Image();
        await new Promise((resolve, reject) => {
            img.onload = resolve;
            img.onerror = () => reject(new Error('capture impossible'));
            img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
        });
        const canvas = document.createElement('canvas');
        canvas.width = Math.max(1, Math.round(w * scale));
        canvas.height = Math.max(1, Math.round(h * scale));
        canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
        blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    }

    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
}
