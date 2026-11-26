// Pont JS de Wpflow.Blazor : mesure des nodes et des handles, molette, suivi du pointeur pendant un geste,
// pincement tactile, touches du navigateur à neutraliser, export d'image. Tout le reste (état, gestes, rendu) est en C#.

const roots = new WeakMap();

// Contrôles HTML d'un node : un appui dessus ne démarre pas de geste (le node est seulement sélectionné).
const INTERACTIVE = 'input,textarea,select,button,a,[contenteditable="true"]';
// Panneaux et barres d'outils posés sur le graphe : un appui dessus ne concerne pas le graphe.
const OVERLAY = '.wf-panel,.wf-toolbar,.wf-selection-toolbar';
const TEXT_INPUT ='input,textarea,select,[contenteditable="true"]';
// Déplacement (px) au-delà duquel le pointeur est capturé ; en deçà, c'est un clic ou un double-clic.
const CAPTURE_DISTANCE = 4;

export function attach(root, dotnet) {
    const s = {
        root, dotnet,
        nodes: new Map(),      // id -> élément
        pending: new Set(),    // ids à remesurer
        soft: new Set(),       // parmi eux, ceux dont seul le contenu a changé : envoyés si la mesure diffère
        sent: new Map(),       // id -> dernière mesure envoyée (texte)
        mutations: new Map(),  // id -> observateur du contenu du node
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
        if (!handled && !s.options.preventScrolling) { // la page défile ; l'application en est tout de même prévenue
            if (s.options.paneScroll) dotnet.invokeMethodAsync('OnWheel', e.clientX, e.clientY, e.deltaX, e.deltaY, e.ctrlKey, e.shiftKey, e.metaKey, e.deltaMode);
            return;
        }
        e.preventDefault();
        sendRect(false);
        dotnet.invokeMethodAsync('OnWheel', e.clientX, e.clientY, e.deltaX, e.deltaY, e.ctrlKey, e.shiftKey, e.metaKey, e.deltaMode);
    };
    root.addEventListener('wheel', s.onWheel, { passive: false });

    // Dépôt : sans « dragover » annulé, le navigateur ne lève jamais « drop ». Fait ici : l'attribut preventDefault de
    // Blazor reste sans effet quand aucun gestionnaire n'écoute cet évènement, et en écouter un enverrait un message
    // par mouvement sur Blazor Server.
    s.onDragOver = (e) => e.preventDefault();
    root.addEventListener('dragenter', s.onDragOver);
    root.addEventListener('dragover', s.onDragOver);
    root.addEventListener('drop', s.onDragOver); // le navigateur n'ouvre pas ce qui est déposé

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
            if (!inside) flags |= 8; // hors de la zone de saisie : le node reste en place, le glisser déplace la vue
        }
        if (closest('.nopan')) flags |= 2;
        // Contrôle HTML, panneau ou barre d'outils : l'appui n'est pas un geste du graphe (ni clic sur le fond, ni zoom).
        if (closest(INTERACTIVE) || closest(OVERLAY)) flags |= 4;
        // Toujours envoyé : les drapeaux de l'appui précédent ne doivent pas rester en mémoire.
        dotnet.invokeMethodAsync('OnPressInfo', flags);
        // Toujours envoyé pour un node (même vide) : l'appui précédent ne doit pas rester en mémoire.
        if (nodeEl) dotnet.invokeMethodAsync('OnPressData', nodeEl.dataset.nodeId, targetData(t, nodeEl));
        if (flags & 4) return; // contrôle HTML : le geste n'est pas suivi, il garde son comportement

        if (s.pointerId !== null) return;
        sendRect(false);
        s.pointerId = e.pointerId;
        s.downX = e.clientX;
        s.downY = e.clientY;
        s.buttonMask = e.button === 0 ? 1 : e.button === 1 ? 4 : 2;
        s.captured = false;
        if (e.button === 1) e.preventDefault();
    };
    s.onMove = (e) => {
        const p = s.pointers.get(e.pointerId);
        if (p) { p.x = e.clientX; p.y = e.clientY; }
        if (s.pinch) { movePinch(s); return; }
        if (e.pointerId !== s.pointerId) return;
        // Le bouton n'est plus enfoncé sans qu'aucun relâchement soit arrivé (boîte de dialogue, autre application) :
        // le geste est abandonné, sinon il suivrait la souris jusqu'au clic suivant.
        if (e.pointerType !== 'touch' && (e.buttons & s.buttonMask) === 0) {
            s.pointers.delete(e.pointerId);
            finish(e, true);
            return;
        }
        // La capture redirige click et dblclick vers la racine : on ne la prend qu'une fois le pointeur réellement
        // déplacé, pour qu'un clic ou un double-clic atteigne le node ou l'edge visé.
        if (!s.captured && Math.hypot(e.clientX - s.downX, e.clientY - s.downY) > CAPTURE_DISTANCE) {
            s.captured = true;
            try { root.setPointerCapture(e.pointerId); } catch { /* pointeur déjà relâché */ }
        }
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
        finish(e, e.type === 'pointercancel');
    };
    const finish = (e, cancelled) => {
        s.pointerId = null;
        if (s.moveRaf) { cancelAnimationFrame(s.moveRaf); s.moveRaf = 0; }
        try { root.releasePointerCapture(e.pointerId); } catch { /* ignoré */ }
        dotnet.invokeMethodAsync('OnPointerUp', e.clientX, e.clientY, e.shiftKey, cancelled);
    };
    root.addEventListener('pointerdown', s.onDown);
    // Seul « mousedown » porte le nombre de clics ; il suit « pointerdown », donc arrive après l'appui dans la vue.
    s.onPress = (e) => { if (e.button === 0 && e.detail === 2) dotnet.invokeMethodAsync('OnSecondPress'); };
    root.addEventListener('mousedown', s.onPress, true);
    // Suite du geste suivie sur la fenêtre : sans capture, le pointeur peut quitter la vue avant le premier mouvement.
    window.addEventListener('pointermove', s.onMove, true);
    window.addEventListener('pointerup', s.onUp, true);
    window.addEventListener('pointercancel', s.onUp, true);

    // Clavier : les saisies dans un champ ne sont pas traitées par le graphe ; les touches du graphe n'agissent pas sur la page.
    // La propagation n'est pas arrêtée : Blazor délègue les évènements plus haut dans l'arbre, et les @onkeydown de
    // l'application sur ses propres champs ne se déclencheraient jamais. La vue est prévenue avant son propre @onkeydown.
    s.onKey = (e) => {
        const inField = !!(e.target.closest && e.target.closest(TEXT_INPUT));
        dotnet.invokeMethodAsync('OnKeyOrigin', inField);
        if (inField) return;
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
    for (const m of s.mutations.values()) m.disconnect();
    window.removeEventListener('scroll', s.onScroll, true);
    window.removeEventListener('resize', s.onScroll);
    root.removeEventListener('wheel', s.onWheel);
    root.removeEventListener('dragenter', s.onDragOver);
    root.removeEventListener('dragover', s.onDragOver);
    root.removeEventListener('drop', s.onDragOver);
    root.removeEventListener('pointerdown', s.onDown);
    root.removeEventListener('mousedown', s.onPress, true);
    window.removeEventListener('pointermove', s.onMove, true);
    window.removeEventListener('pointerup', s.onUp, true);
    window.removeEventListener('pointercancel', s.onUp, true);
    root.removeEventListener('keydown', s.onKey);
    if (s.raf) cancelAnimationFrame(s.raf);
    roots.delete(root);
}

/** Les attributs data-* de l'élément pressé et de ses parents dans le node (node exclu) ; le plus proche l'emporte. */
function targetData(target, nodeEl) {
    const data = {};
    for (let el = target; el && el !== nodeEl; el = el.parentElement) {
        for (const a of el.attributes || []) {
            if (!a.name.startsWith('data-')) continue;
            const key = a.name.slice(5);
            if (!Object.hasOwn(data, key)) data[key] = a.value;
        }
    }
    return data;
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
    watchContent(s, id, el);
    queue(s, id);
}

// Des handles peuvent être ajoutés, retirés ou déplacés sans que la taille du node change (colonnes réordonnées) :
// le ResizeObserver ne le voit pas. Le style et les classes du node lui-même (déplacement, sélection) sont ignorés.
function watchContent(s, id, el) {
    s.mutations.get(id)?.disconnect();
    const observer = new MutationObserver(records => {
        if (records.some(r => r.target !== el || r.type === 'childList')) queue(s, id, true);
    });
    observer.observe(el, {
        childList: true, subtree: true, characterData: true, attributes: true,
        attributeFilter: ['class', 'style', 'hidden', 'data-handleid', 'data-type', 'data-position',
            'data-connectable', 'data-connectable-start', 'data-connectable-end']
    });
    s.mutations.set(id, observer);
}

export function unobserveNode(root, id) {
    const s = roots.get(root);
    if (!s) return;
    const el = s.nodes.get(id);
    if (el) s.ro.unobserve(el);
    s.nodes.delete(id);
    s.mutations.get(id)?.disconnect();
    s.mutations.delete(id);
    s.sent.delete(id);
}

/** Force une nouvelle mesure (handles ajoutés ou déplacés sans changement de taille du node). */
export function remeasure(root, id) {
    const s = roots.get(root);
    if (s) queue(s, id);
}

function queue(s, id, contentOnly = false) {
    if (!contentOnly) s.soft.delete(id);
    else if (!s.pending.has(id)) s.soft.add(id);
    s.pending.add(id);
    if (s.raf) return;
    s.raf = requestAnimationFrame(() => {
        s.raf = 0;
        const batch = [];
        for (const nid of s.pending) {
            const el = s.nodes.get(nid);
            if (el && el.isConnected) {
                const m = measure(el, nid);
                if (!m) continue;
                const text = JSON.stringify(m, (key, v) => typeof v === 'number' ? Math.round(v * 10) / 10 : v);
                if (s.soft.has(nid) && s.sent.get(nid) === text) continue;
                s.sent.set(nid, text);
                batch.push(m);
            }
        }
        s.pending.clear();
        s.soft.clear();
        for (const message of messages(batch)) s.dotnet.invokeMethodAsync('OnMeasured', message);
    });
}

// Blazor Server refuse un message de plus de 32 Ko (réglage par défaut de SignalR) et coupe alors le circuit : quelques
// dizaines de nodes à plusieurs handles suffisent. La marge couvre l'enveloppe et les identifiants non ASCII.
const MESSAGE_LIMIT = 12000;

/** Découpe les mesures en messages ; un node trop gros à lui seul est coupé par handles (`partial` : la suite arrive). */
function messages(batch) {
    const result = [];
    let current = [], size = 0;
    const add = (part) => {
        const length = JSON.stringify(part).length;
        if (current.length && size + length > MESSAGE_LIMIT) {
            result.push(current);
            current = [];
            size = 0;
        }
        current.push(part);
        size += length;
    };

    for (const node of batch) {
        if (JSON.stringify(node).length <= MESSAGE_LIMIT) {
            add(node);
            continue;
        }

        let handles = [], length = 0;
        for (const handle of node.handles) {
            const l = JSON.stringify(handle).length;
            if (handles.length && length + l > MESSAGE_LIMIT / 2) {
                add({ ...node, handles, partial: true });
                handles = [];
                length = 0;
            }
            handles.push(handle);
            length += l;
        }
        add({ ...node, handles });
    }

    if (current.length) result.push(current);
    return result;
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
