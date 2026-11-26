// L'anneau de solides du rendu : port de CubeField.cs (démo WPF), mêmes calculs, dessiné sur un canvas.
// L'animation tourne ici, sans aller-retour avec .NET ; Blazor ne transmet que les réglages.
(() => {
    const rings = new WeakMap();
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');

    const cube = {
        verts: [[-1, -1, 1], [1, -1, 1], [1, 1, 1], [-1, 1, 1], [-1, -1, -1], [1, -1, -1], [1, 1, -1], [-1, 1, -1]],
        faces: [[0, 1, 2, 3], [5, 4, 7, 6], [4, 0, 3, 7], [1, 5, 6, 2], [3, 2, 6, 7], [4, 5, 1, 0]]
    };
    const pyramid = {
        verts: [[-1, -1, 1], [1, -1, 1], [1, -1, -1], [-1, -1, -1], [0, 1.3, 0]],
        faces: [[0, 1, 4], [1, 2, 4], [2, 3, 4], [3, 0, 4], [3, 2, 1, 0]]
    };

    const rotX = ([x, y, z], a) => [x, y * Math.cos(a) - z * Math.sin(a), y * Math.sin(a) + z * Math.cos(a)];
    const rotY = ([x, y, z], a) => [x * Math.cos(a) + z * Math.sin(a), y, -x * Math.sin(a) + z * Math.cos(a)];
    const rotZ = ([x, y, z], a) => [x * Math.cos(a) - y * Math.sin(a), x * Math.sin(a) + y * Math.cos(a), z];

    const length = Math.hypot(-0.4, 0.8, 0.6);
    const light = [-0.4 / length, 0.8 / length, 0.6 / length];

    function draw(canvas, ring, t) {
        const width = canvas.clientWidth, height = canvas.clientHeight;
        if (width <= 0 || height <= 0) return;

        // La taille à l'écran tient compte du zoom de la vue : le dessin reste net.
        const screen = canvas.getBoundingClientRect().width / width * window.devicePixelRatio || 1;
        const pixelWidth = Math.round(width * screen), pixelHeight = Math.round(height * screen);
        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
            canvas.width = pixelWidth;
            canvas.height = pixelHeight;
        }

        const context = canvas.getContext('2d');
        context.setTransform(pixelWidth / width, 0, 0, pixelHeight / height, 0, 0);
        context.clearRect(0, 0, width, height);
        context.strokeStyle = '#0b1a2b';
        context.lineWidth = 0.7;
        context.lineJoin = 'round';

        const rgb = [1, 3, 5].map(i => parseInt(ring.color.slice(i, i + 2), 16));
        const shape = ring.pyramids ? pyramid : cube;
        const scale = Math.min(width, height) * (0.10 + ring.shownZoom * 0.34);
        const cx = width / 2, cy = height / 2 - 2;
        const spin = t * 0.45, tilt = 0.75;

        const items = ring.solids.map(([x, y, z, phase, speed, size], index) => {
            const around = index * Math.PI * 2 / ring.solids.length;
            const radius = 2.0 + x * 0.4;
            const position = [Math.cos(around) * radius, y * 0.4 + Math.sin(t * speed + phase) * 0.12,
                Math.sin(around) * radius + z * 0.2];
            return { phase, speed, size, p: rotX(rotY(position, spin), tilt) };
        }).sort((a, b) => a.p[2] - b.p[2]); // les plus lointains d'abord

        for (const { phase, speed, size, p } of items) {
            const own = t * speed + phase;
            const world = shape.verts.map(vertex => {
                const scaled = vertex.map(c => c * size * 1.5);
                const turned = rotX(rotY(rotZ(rotY(scaled, own), own * 0.6), spin), tilt);
                return [turned[0] + p[0], turned[1] + p[1], turned[2] + p[2]];
            });

            for (const face of shape.faces) {
                const a = world[face[0]], b = world[face[1]], c = world[face[2]];
                const u = [b[0] - a[0], b[1] - a[1], b[2] - a[2]];
                const v = [c[0] - a[0], c[1] - a[1], c[2] - a[2]];
                const n = [u[1] * v[2] - u[2] * v[1], u[2] * v[0] - u[0] * v[2], u[0] * v[1] - u[1] * v[0]];
                const size3 = Math.hypot(...n);
                if (size3 < 1e-9 || n[2] <= 0) continue; // face dos à la caméra

                const lit = (n[0] * light[0] + n[1] * light[1] + n[2] * light[2]) / size3;
                const brightness = 0.55 + 0.6 * Math.max(0, lit);
                context.fillStyle = `rgb(${rgb.map(k => Math.min(255, Math.floor(k * brightness))).join(',')})`;
                context.beginPath();
                face.forEach((i, k) => context[k === 0 ? 'moveTo' : 'lineTo'](cx + world[i][0] * scale, cy - world[i][1] * scale));
                context.closePath();
                context.fill();
                context.stroke();
            }
        }
    }

    function start(canvas, ring) {
        const origin = performance.now();
        let last = origin;
        const frame = now => {
            if (!canvas.isConnected) {
                rings.delete(canvas);
                return;
            }

            // Inertie du zoom, indépendante de la cadence.
            const dt = Math.min(Math.max(now - last, 0) / 1000, 0.1);
            last = now;
            ring.shownZoom = reduced.matches ? ring.zoom : ring.shownZoom + (ring.zoom - ring.shownZoom) * (1 - Math.exp(-8 * dt));
            draw(canvas, ring, reduced.matches ? 0 : Math.max(now - origin, 0) / 1000);
            requestAnimationFrame(frame);
        };
        requestAnimationFrame(frame);
    }

    window.wpsite = window.wpsite || {};
    // solids : [x, y, z, phase, vitesse, taille] ; zoom entre 0 et 1.
    window.wpsite.ring = (canvas, solids, color, pyramids, zoom) => {
        let ring = rings.get(canvas);
        if (!ring) {
            ring = { shownZoom: zoom };
            rings.set(canvas, ring);
            start(canvas, ring);
        }
        Object.assign(ring, { solids, color, pyramids, zoom });
    };
})();
