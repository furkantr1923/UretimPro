// Genel arayüz: tema, menü, sayaçlar, canlı bildirimler
(function () {
    const csrf = document.querySelector('meta[name="csrf"]');
    window.CsrfToken = csrf ? csrf.content : '';

    // ---------- Tema ----------
    function temaUygula(tema, grafikYenile) {
        document.documentElement.dataset.tema = tema;
        try { localStorage.setItem('tema', tema); } catch (e) { }
        document.querySelectorAll('[data-tema-ikon]').forEach(function (i) {
            i.classList.toggle('gizli', i.dataset.temaIkon !== tema);
        });
        if (grafikYenile !== false && window.Grafik) window.Grafik.yenile();
    }

    document.addEventListener('click', function (e) {
        const tema = e.target.closest('[data-tema-degistir]');
        if (tema) {
            const yeni = document.documentElement.dataset.tema === 'karanlik' ? 'acik' : 'karanlik';
            temaUygula(yeni);
            if (window.Efekt) Efekt.cal('tik');
        }
        const menu = e.target.closest('[data-menu-ac]');
        if (menu) document.getElementById('yanMenu').classList.toggle('acik');

        const kapat = e.target.closest('[data-kapat]');
        if (kapat) kapat.closest('.mesaj').remove();

        // Soy ağacında dal aç / kapat
        const dal = e.target.closest('[data-dal]');
        if (dal) {
            const li = dal.closest('li');
            if (li) {
                li.classList.toggle('kapali');
                dal.setAttribute('aria-expanded', li.classList.contains('kapali') ? 'false' : 'true');
                if (window.Efekt) Efekt.cal('tik');
            }
        }

        const demo = e.target.closest('[data-demo-kullanici]');
        if (demo) {
            document.getElementById('KullaniciAdi').value = demo.dataset.demoKullanici;
            document.getElementById('Sifre').value = demo.dataset.demoSifre;
            if (window.Efekt) Efekt.cal('tik');
        }
    });

    // ---------- Sayı sayacı (0'dan hedefe) ----------
    function sayaclar() {
        document.querySelectorAll('[data-sayac]').forEach(function (s) {
            const hedef = parseFloat(s.dataset.sayac.replace(',', '.'));
            if (isNaN(hedef)) return;
            const basamak = parseInt(s.dataset.basamak || '0', 10);
            const onEk = s.dataset.onek || '';
            const sonEk = s.dataset.sonek || '';
            const bas = performance.now();
            const sure = 1300;
            (function adim(simdi) {
                const t = Math.min(1, (simdi - bas) / sure);
                const e = 1 - Math.pow(1 - t, 3);
                s.textContent = onEk + (hedef * e).toLocaleString('tr-TR', { minimumFractionDigits: basamak, maximumFractionDigits: basamak }) + sonEk;
                if (t < 1) requestAnimationFrame(adim);
            })(bas);
        });
    }

    // ---------- Bildirim kutucukları ----------
    function tost(b) {
        const kutu = document.getElementById('tostlar');
        if (!kutu) return;
        const t = document.createElement(b.link ? 'a' : 'div');
        t.className = 'tost ' + (b.seviye || 'bilgi');
        if (b.link) t.href = b.link;
        const ic = document.createElement('div');
        const bas = document.createElement('strong');
        bas.textContent = b.baslik;
        const msj = document.createElement('span');
        msj.textContent = b.mesaj;
        ic.appendChild(bas);
        ic.appendChild(msj);
        t.appendChild(ic);
        kutu.appendChild(t);
        setTimeout(function () {
            t.classList.add('gidiyor');
            setTimeout(function () { t.remove(); }, 400);
        }, 7000);
    }
    window.Tost = tost;

    function zilArtir() {
        const zil = document.getElementById('zilDugme');
        if (!zil) return;
        let sayi = zil.querySelector('.zil-sayi');
        if (!sayi) {
            sayi = document.createElement('span');
            sayi.className = 'zil-sayi';
            sayi.textContent = '0';
            zil.appendChild(sayi);
        }
        sayi.textContent = String((parseInt(sayi.textContent, 10) || 0) + 1);
        sayi.classList.remove('yeni');
        void sayi.offsetWidth;
        sayi.classList.add('yeni');
        zil.classList.remove('zil-sallan');
        void zil.offsetWidth;
        zil.classList.add('zil-sallan');
    }

    function akisaEkle(m) {
        const akis = document.getElementById('canliAkis');
        if (!akis) return;
        if (akis.dataset.makine && akis.dataset.makine !== m.makine) return;
        const oge = document.createElement('div');
        oge.className = 'akis-oge yeni';
        const sol = document.createElement('span');
        const seri = document.createElement('span');
        seri.className = 'mono';
        seri.textContent = m.seriNo;
        sol.appendChild(seri);
        sol.appendChild(document.createTextNode(' · ' + m.adim + ' · ' + m.makine + ' · ' + m.kullanici));
        const zaman = document.createElement('time');
        zaman.textContent = m.zaman.slice(0, 5);
        oge.appendChild(sol);
        oge.appendChild(zaman);
        akis.insertBefore(oge, akis.firstChild);
        while (akis.children.length > 8) akis.removeChild(akis.lastChild);
    }

    // ---------- Canlı bağlantı (Server-Sent Events) ----------
    function canliBaglan() {
        if (!document.body.dataset.canli || !window.EventSource) return;
        const kaynak = new EventSource('/bildirim/akis');
        kaynak.onmessage = function (olay) {
            let m;
            try { m = JSON.parse(olay.data); } catch (e) { return; }
            // Sayfaya özel scriptler (fabrika planı vb.) için olay
            document.dispatchEvent(new CustomEvent('canli', { detail: m }));
            if (m.tur === 'bildirim') {
                tost(m);
                zilArtir();
                if (window.Efekt) Efekt.cal(m.seviye === 'kritik' ? 'alarm' : 'bildirim');
                if (m.seviye === 'kritik' && window.Efekt) Efekt.alarm();
            } else if (m.tur === 'islem') {
                akisaEkle(m);
            }
        };
    }

    document.addEventListener('DOMContentLoaded', function () {
        sayaclar();
        canliBaglan();
        temaUygula(document.documentElement.dataset.tema || 'acik', false);
        const efekt = document.body.dataset.efekt;
        if (efekt && window.Efekt) setTimeout(function () { Efekt.uygula(efekt); }, 250);
    });
})();
