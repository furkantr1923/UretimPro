// Ses efektleri (Web Audio ile üretilir, dosya gerektirmez) ve konfeti
window.Efekt = (function () {
    let ses = null;

    function baglam() {
        const C = window.AudioContext || window.webkitAudioContext;
        if (!C) return null;
        if (!ses) ses = new C();
        if (ses.state === 'suspended') ses.resume();
        return ses;
    }

    // Tarayıcılar sesi ilk kullanıcı etkileşiminden sonra açar
    ['pointerdown', 'keydown'].forEach(function (olay) {
        window.addEventListener(olay, function () { baglam(); }, { once: true, capture: true });
    });

    function ton(frekans, baslangic, sure, tip, guc) {
        const c = baglam();
        if (!c) return;
        const o = c.createOscillator();
        const g = c.createGain();
        o.type = tip || 'sine';
        o.frequency.value = frekans;
        const t = c.currentTime + baslangic;
        g.gain.setValueAtTime(0.0001, t);
        g.gain.exponentialRampToValueAtTime(guc || 0.2, t + 0.015);
        g.gain.exponentialRampToValueAtTime(0.0001, t + sure);
        o.connect(g);
        g.connect(c.destination);
        o.start(t);
        o.stop(t + sure + 0.05);
    }

    const sesler = {
        bip: function () { ton(1568, 0, 0.09, 'square', 0.08); ton(2093, 0.09, 0.12, 'square', 0.07); },
        tik: function () { ton(880, 0, 0.05, 'triangle', 0.08); },
        hata: function () { ton(220, 0, 0.18, 'sawtooth', 0.12); ton(180, 0.2, 0.28, 'sawtooth', 0.12); },
        basari: function () { ton(523, 0, 0.14); ton(659, 0.11, 0.14); ton(784, 0.22, 0.14); ton(1047, 0.33, 0.35); },
        bildirim: function () { ton(988, 0, 0.1); ton(1319, 0.1, 0.22); },
        giris: function () { ton(392, 0, 0.12); ton(587, 0.1, 0.12); ton(784, 0.2, 0.28); },
        alarm: function () {
            for (let i = 0; i < 5; i++) {
                ton(1046, i * 0.34, 0.16, 'sawtooth', 0.1);
                ton(784, i * 0.34 + 0.17, 0.16, 'sawtooth', 0.1);
            }
        }
    };

    function konfeti(adet) {
        const tuval = document.createElement('canvas');
        tuval.className = 'konfeti-tuval';
        const oran = window.devicePixelRatio || 1;
        tuval.width = innerWidth * oran;
        tuval.height = innerHeight * oran;
        document.body.appendChild(tuval);
        const c = tuval.getContext('2d');
        c.scale(oran, oran);

        const renkler = ['#B8733A', '#D58E52', '#1E7A4C', '#4CC487', '#F6E7D8', '#2358A3', '#E9B04B'];
        const parcalar = [];
        for (let i = 0; i < (adet || 180); i++) {
            const soldan = i % 2 === 0;
            parcalar.push({
                x: soldan ? -10 : innerWidth + 10,
                y: innerHeight * 0.7,
                vx: (soldan ? 1 : -1) * (6 + Math.random() * 10),
                vy: -(10 + Math.random() * 14),
                boy: 6 + Math.random() * 8,
                aci: Math.random() * Math.PI,
                donus: (Math.random() - 0.5) * 0.35,
                renk: renkler[i % renkler.length],
                sekil: Math.random() > 0.7 ? 'daire' : 'kare'
            });
        }

        const bas = performance.now();
        const SURE = 3200;
        (function ciz(simdi) {
            const gecen = simdi - bas;
            c.clearRect(0, 0, innerWidth, innerHeight);
            parcalar.forEach(function (p) {
                p.vy += 0.32;
                p.vx *= 0.985;
                p.x += p.vx;
                p.y += p.vy;
                p.aci += p.donus;
                c.save();
                c.globalAlpha = Math.max(0, 1 - gecen / SURE);
                c.translate(p.x, p.y);
                c.rotate(p.aci);
                c.fillStyle = p.renk;
                if (p.sekil === 'daire') { c.beginPath(); c.arc(0, 0, p.boy / 2.5, 0, Math.PI * 2); c.fill(); }
                else c.fillRect(-p.boy / 2, -p.boy / 4, p.boy, p.boy / 2);
                c.restore();
            });
            if (gecen < SURE) requestAnimationFrame(ciz); else tuval.remove();
        })(bas);
    }

    function alarmGoster() {
        const k = document.createElement('div');
        k.className = 'alarm-katman';
        document.body.appendChild(k);
        setTimeout(function () { k.remove(); }, 3200);
    }

    function basariFlas() {
        const k = document.createElement('div');
        k.className = 'basari-flas';
        document.body.appendChild(k);
        setTimeout(function () { k.remove(); }, 900);
    }

    return {
        cal: function (ad) { try { if (sesler[ad]) sesler[ad](); } catch (e) { } },
        konfeti: konfeti,
        alarm: alarmGoster,
        flas: basariFlas,
        // Sayfa yüklenince sunucunun istediği efekt (TempData["Efekt"])
        uygula: function (ad) {
            if (!ad) return;
            if (ad === 'konfeti') { konfeti(); this.cal('basari'); }
            else if (ad === 'alarm') { alarmGoster(); this.cal('alarm'); }
            else if (ad === 'basari') { basariFlas(); this.cal('basari'); }
            else this.cal(ad);
        }
    };
})();
