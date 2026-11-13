// Elektronik imza alanı: parmak, kalem veya fare ile çizim; 400x140 koordinatlı SVG path olarak gönderilir
(function () {
    const GEN = 400, YUK = 140;

    function kur(tuval) {
        const form = tuval.closest('form');
        const gizli = form ? form.querySelector('input[name="imza"]') : null;
        const ctx = tuval.getContext('2d');
        let yol = '';
        let ciziyor = false;
        let noktaSayisi = 0;

        function boyutla() {
            const oran = window.devicePixelRatio || 1;
            const r = tuval.getBoundingClientRect();
            tuval.width = Math.max(1, Math.round(r.width * oran));
            tuval.height = Math.max(1, Math.round(r.width * YUK / GEN * oran));
            tuval.style.height = (r.width * YUK / GEN) + 'px';
            yenidenCiz();
        }

        function olcek() { return tuval.width / GEN; }

        function kalem() {
            ctx.lineWidth = 2.4 * olcek();
            ctx.lineCap = 'round';
            ctx.lineJoin = 'round';
            ctx.strokeStyle = getComputedStyle(document.documentElement).getPropertyValue('--yazi').trim() || '#13201A';
        }

        function yenidenCiz() {
            ctx.clearRect(0, 0, tuval.width, tuval.height);
            if (!yol) return;
            kalem();
            const o = olcek();
            const parcalar = yol.trim().split(' ');
            ctx.beginPath();
            for (let i = 0; i < parcalar.length; i += 3) {
                const k = parcalar[i], x = parseFloat(parcalar[i + 1]) * o, y = parseFloat(parcalar[i + 2]) * o;
                if (k === 'M') ctx.moveTo(x, y); else ctx.lineTo(x, y);
            }
            ctx.stroke();
        }

        function nokta(e) {
            const r = tuval.getBoundingClientRect();
            const x = Math.max(0, Math.min(GEN, (e.clientX - r.left) / r.width * GEN));
            const y = Math.max(0, Math.min(YUK, (e.clientY - r.top) / r.height * YUK));
            return { x: x.toFixed(1), y: y.toFixed(1) };
        }

        function yaz() {
            if (gizli) gizli.value = noktaSayisi >= 8 ? yol.trim() : '';
            tuval.classList.toggle('dolu', noktaSayisi >= 8);
        }

        tuval.addEventListener('pointerdown', function (e) {
            e.preventDefault();
            tuval.setPointerCapture(e.pointerId);
            ciziyor = true;
            const p = nokta(e);
            yol += ' M ' + p.x + ' ' + p.y;
            kalem();
            ctx.beginPath();
            ctx.moveTo(p.x * olcek(), p.y * olcek());
        });
        tuval.addEventListener('pointermove', function (e) {
            if (!ciziyor) return;
            const p = nokta(e);
            yol += ' L ' + p.x + ' ' + p.y;
            noktaSayisi++;
            ctx.lineTo(p.x * olcek(), p.y * olcek());
            ctx.stroke();
        });
        ['pointerup', 'pointercancel', 'pointerleave'].forEach(function (olay) {
            tuval.addEventListener(olay, function () {
                if (!ciziyor) return;
                ciziyor = false;
                yaz();
            });
        });

        const temizle = form ? form.querySelector('[data-imza-temizle]') : null;
        if (temizle) temizle.addEventListener('click', function () {
            yol = '';
            noktaSayisi = 0;
            yenidenCiz();
            yaz();
        });

        if (form) form.addEventListener('submit', function (e) {
            if (!gizli || !gizli.value) {
                e.preventDefault();
                tuval.classList.add('hata-sallan');
                setTimeout(function () { tuval.classList.remove('hata-sallan'); }, 600);
                if (window.Efekt) Efekt.cal('hata');
                if (window.Tost) Tost({ baslik: 'İmza gerekli', mesaj: 'Lütfen imza alanına imzanızı çizin.', seviye: 'uyari' });
            }
        });

        window.addEventListener('resize', boyutla);
        boyutla();
    }

    document.querySelectorAll('canvas[data-imza]').forEach(kur);
})();
