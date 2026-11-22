# PEAK Local Co-op 0.1.12

Mod eksperimental untuk PEAK Windows versi 3.1.0, Steam build **26542162**. Mod memakai satu proses PEAK, satu window dan satu room Photon offline. Setiap kursi memakai karakter, inventory, input dan sudut pandangnya sendiri di dunia yang sama.

## Unduh dan mulai

- [Unduh ZIP v0.1.12](https://github.com/AlphaGambit/PEAKLocalCoop/releases/download/v0.1.12/PEAKLocalCoop-0.1.12.zip)
- [Tutorial pemasangan, cara main, dan kontrol controller](docs/TUTORIAL.md)

Ekstrak ZIP di luar folder Steam, tutup PEAK, lalu jalankan `Install or Repair Local Co-op.cmd`. Setelah selesai, buka `Play Local Co-op DX11.cmd`. P1 memakai keyboard dan mouse. P2–P4 menekan **Start/Menu** di controller masing-masing.

## Build

Butuh instalasi Steam PEAK yang sah, .NET SDK 8 (diuji dengan 8.0.422), dan core BepInEx 5.4.23.3 beserta `0Harmony.dll` dari paket yang dipin. DLL PEAK dan Unity dibaca langsung dari `PEAK_Data/Managed`. [BepInExPack PEAK 5.4.75301](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/v/5.4.75301/).

Jalankan perintah berikut dari PowerShell dengan path instalasi yang sesuai.

```powershell
$peakDir = 'C:\Program Files (x86)\Steam\steamapps\common\PEAK'
$bepCore = Join-Path $peakDir 'BepInEx\core'
dotnet build .\PeakLocalCoop.csproj -c Release "-p:PeakDir=$peakDir" "-p:BepInExCore=$bepCore"
```

`BepInExCore` juga bisa menunjuk ke folder core hasil ekstrak paket sebelum loader dipasang. Hasil build berada di `bin/Release/netstandard2.1/PeakLocalCoop.dll`. Project tidak menyalin dependensi game ke output.

## Pasang dan mainkan

1. Ekstrak seluruh ZIP ke folder di luar library Steam, misalnya Documents. Tutup PEAK dan jalankan `Install or Repair Local Co-op.cmd`. Pemasang mencari instalasi Steam PEAK dan mengambil BepInExPack PEAK 5.4.75301 dari Thunderstore saat cache loader belum lengkap. Jika Windows menolak penulisan ke folder Steam, jalankan pemasang dengan akun yang punya izin tulis. [BepInExPack PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/v/5.4.75301/).
2. Buka PEAK sekali dari Steam atau profil tersebut. Di menu utama, pilih **Local Co-op** di antara **Play Offline** dan **Settings** pada menu berbahasa Inggris. P1 memakai keyboard dan mouse. Tekan **Start/Menu** pada setiap controller yang akan mengisi P2–P4. Kartu lobi menampilkan tanda centang untuk slot tersambung. Controller yang belum mengisi kursi ditampilkan di bawah kartu.
3. Klik **Masuk Airport**. P1 memakai kiosk dan boarding pass biasa untuk mulai pendakian. Ikuti binding PEAK di masing-masing perangkat. HUD setiap tamu memakai bar stamina, segmen cedera dan efek status, slot item, serta roda ransel dari PEAK. Kartu lobi menampilkan ikon keyboard dengan mouse untuk P1 dan ikon gamepad untuk kursi controller. Di panel ransel pemain tambahan, pakai D-pad untuk memilih, tombol bawah untuk mengonfirmasi, tombol kanan untuk menutup.
4. Setelah campfire yang memajukan segmen menyala, tunggu pemberitahuan checkpoint. Controller bebas bisa menekan **Start/Menu** selama party berada di area camp aktif dan ada kursi kosong. Setiap pemain baru memakai prefab baru dan inventory kosong. Perlengkapan tantangan khusus saat mulai run mengikuti jalur PEAK. Join di checkpoint memakai kondisi normal tanpa menyalin perlengkapan.
5. Sambungkan kembali controller lama seperti biasa. Jika perangkat baru perlu menggantikan satu controller yang putus, tekan **Start/Menu** pada pengganti. Jika beberapa kursi terputus, P1 memilih P2/P3/P4 lewat **F6/F7/F8**, lalu pemain menekan **Start/Menu** pada pengganti. Karakter yang terputus tetap berada di dunia.

Jika memakai profil mod manager, pasang BepInExPack PEAK dan salin `release/PeakLocalCoop.dll` ke `BepInEx/plugins/PeakLocalCoop/` **di dalam profil**. Jalankan game dari profil itu. Penginstal ZIP dan launcher DX11 mengelola instalasi Steam langsung.

Untuk mematikan fitur dari konfigurasi, ubah `Enabled = false` di `BepInEx/config/local.peak.coop.cfg` dan mulai ulang. Untuk melepas mod, tutup PEAK dan hapus `BepInEx/plugins/PeakLocalCoop/`. BepInEx dapat tetap dipakai oleh mod lain. Untuk menghapus salinan pemulihan, hapus juga `%LOCALAPPDATA%\PEAKLocalCoop` dan folder hasil ekstrak ZIP. Launcher dari ZIP akan memasang DLL kembali jika dijalankan setelah pencopotan. Jika loader dipasang khusus untuk mod ini, lepaskan paket loader dari profil tersebut atau hapus file loader yang dipasang secara manual berdasarkan daftar isi paket.

### Binding bawaan controller

| Kondisi | Kontrol |
| --- | --- |
| Memegang Rope Spool | Tahan RB/R1 untuk mengulur dan LB/L1 untuk menggulung. Panjang muncul pada panel sendiri. Arahkan ke permukaan dan tahan RT/R2 untuk memasang. |
| Hotbar | D-pad kiri dan kanan pindah slot. D-pad bawah mengosongkan tangan, atau kembali ke pilihan terakhir jika tangan sudah kosong. |
| Emote | Tahan D-pad atas. Pilih dengan stik kanan, pakai kiri/kanan untuk membuka halaman lain, lalu lepaskan D-pad atas untuk memainkan emote. |
| Ping | Tekan stik kanan (R3) untuk menandai permukaan di pusat panel. P1 bisa memakai klik tengah mouse. |
| Menonton setelah mati atau pingsan | D-pad kiri dan kanan memilih scout hidup. RB/R1 mendekatkan kamera dan LB/L1 menjauhkannya. |
| Pingsan dan mati | Bar PEAK di panel sendiri menampilkan sisa waktu. Saat benar-benar mati dan masih ada scout hidup untuk ditonton, hantu muncul untuk pemain lain dan hilang saat revive. |

## Buka Local Co-op dengan Direct3D 11

1. Tutup PEAK dan biarkan Steam terbuka.
2. Klik dua kali `Play Local Co-op DX11.cmd` dari folder proyek atau dari ZIP yang sudah diekstrak. Launcher membuka lobi Local Co-op secara langsung.
3. Masuk bersama controller seperti biasa. Tombol **Keluar** di lobi atau keluar dari sesi menutup proses khusus ini. Buka game seperti biasa dari Steam untuk masuk ke menu dengan mode solo dan online.

Launcher memilih Direct3D 11 saat proses mulai. Resolusi, render scale, preset, bayangan, dan pengaturan kualitas tetap mengikuti PEAK. Launcher mencari instalasi PEAK di library Steam yang terdaftar. Untuk menentukan folder pemasangan secara eksplisit, berikan path folder PEAK sebagai argumen penginstal. Lihat [hasil benchmark](docs/PERFORMANCE.md).

## Pemulihan setelah update Steam

- Pemasang menyimpan salinan DLL mod dan file loader terverifikasi di `%LOCALAPPDATA%\PEAKLocalCoop`. Simpan folder hasil ekstrak ZIP di luar instalasi game.
- Setiap kali `Play Local Co-op DX11.cmd` dijalankan, launcher memulihkan file mod dan loader yang hilang dari salinan itu. Untuk memulihkan file lalu membuka PEAK biasa lewat Steam, jalankan `Install or Repair Local Co-op.cmd` terlebih dahulu.
- Konfigurasi yang sudah ada dan mod lain dipertahankan. Launcher berhenti jika menemukan binary loader yang berbeda. Penginstal dapat memasang versi DLL yang ada di ZIP setelah mencadangkan DLL Local Co-op yang terpasang sebelumnya. Saat mengunduh loader, penginstal memeriksa SHA-256; arsip resmi juga dapat diberikan lewat parameter PowerShell `-LoaderArchive` untuk pemasangan offline.
- Launcher memeriksa fingerprint build PEAK sebelum membuka lobi Local Co-op. Jika file integrasi PEAK berubah, salinan mod tetap tersedia dan Local Co-op menunggu rilis yang kompatibel. Build guard di dalam mod juga memeriksa kompatibilitas saat PEAK dibuka lewat Steam.
- Pengujian di Windows menyimulasikan update dengan menghapus DLL mod dan dua file loader dari instalasi uji. Pemulihan dari cache, konfigurasi dan mod lain yang tetap utuh, penggantian versi dengan cadangan, dan penolakan peluncuran pada fingerprint berbeda sudah diperiksa. Steam belum dipakai untuk menguji update game sungguhan.

## Bagikan lewat GitHub

Lampirkan `PEAKLocalCoop-0.1.12.zip` sebagai aset GitHub Release agar pemain mendapat DLL mod dan skrip instalasi bersama source. ZIP berisi source, DLL mod, aset mod, dan dokumentasi. File game beserta binary loader dipasang dari sumber masing-masing. [Panduan GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases).

## Cakupan

- Progress campfire dan perubahan biome berlangsung di dunia bersama selama proses berjalan. Sesi Local Co-op menonaktifkan penulisan dan penghapusan `quicksave.peak` vanilla, sehingga quicksave solo yang sudah ada tetap dapat dipakai di luar sesi mod. Simpan dan lanjutkan party setelah menutup aplikasi belum didukung.
- Kamera memakai viewport langsung di URP. Audio posisi memakai listener dari kamera P1. Efek lingkungan dan UI sinematik tertentu tetap memakai perspektif host. Menu run yang dibagikan, termasuk boarding dan akhir run, memakai kontrol P1.
- Join dalam run dibuka pada jalur campfire yang diaudit, di luar mini run, Peak dan Void. Kiln hanya dapat membuka join saat lava belum aktif. Jika party meninggalkan radius setelah gate terbuka, api yang sama tidak membuka join kedua kali. Penyambungan ulang perangkat tetap tersedia.
- ID kursi berupa GUID yang terpisah dari nama tampilan, perangkat, Steam ID dan nomor actor Photon. Fase ini lokal. Sinkronisasi online, klien tanpa mod, undangan dan proximity voice tidak dipakai oleh mod.

## Diagnostik

`BepInEx/LogOutput.log` melaporkan fingerprint build, pengaitan kursi, scene, kegagalan join dan hasil probe. Argumen peluncuran khusus berikut memberi input sintetis dan keluar ketika pengujian selesai. Gunakan satu argumen per peluncuran.

| Argumen | Kegunaan |
| --- | --- |
| `-peak-coop-world-check -logFile "C:\path\world.log"` | Menguji benturan perangkap, pemulihan item dari flag personal dan visual marker per panel di PEAK. |
| `-peak-coop-logic-check -logFile "C:\path\logic.log"` | Menjalankan uji empat kursi, inventory, empat arah D-pad, RB/LB, emote, ping, efek area, bidik item, checkpoint dan akhir run di game terpasang. |
| `-peak-coop-feed-check` | Memeriksa slot pemberi dan inventory penerima setelah bandage dan makanan diberikan dari P1 ke P2 dan sebaliknya. |
| `-peak-coop-item-check -logFile "C:\path\items.log"` | Menguji sasaran bidik dan titik tembak Rope Cannon P2 di pulau. |
| `-peak-coop-probe` | Memeriksa dua prefab native, pemisahan gerak/lihat dan pickup dasar di Airport. |
| `-peak-coop-menu-check -logFile "C:\path\menu.log"` | Memeriksa tombol di menu utama, fokus controller di lobi dan masuk Airport, dengan tangkapan layar. |
| `-peak-coop-slice -logFile "C:\path\slice.log"` | Menjalankan alur lobby, Airport, pulau, inventory, checkpoint, revive dan cuplikan layar. Membuat CSV perbandingan bayangan dan internal render scale bila sampai ke empat pemain. |
| `-peak-coop-perf-api -logFile "C:\path\perf.log"` | Mengukur dua pemain pada dua arah pandang di checkpoint dengan API yang dipilih saat game diluncurkan. |
| `-peak-coop-bench -logFile "C:\path\bench.log"` | Mengukur 1 hingga 4 kursi bertahap pada checkpoint yang sama, dengan arah kamera searah dan berbeda. Menulis `bench.frames.csv` di samping log. |

## Pemeliharaan

Baca [audit build](docs/AUDIT.md), [catatan integrasi](docs/INTEGRATION.md) dan [hasil verifikasi dan performa](docs/VERIFICATION.md).
