# Benchmark 1080p versi 0.1.11

Pengukuran 22 November 2026 pada PEAK build 26542162 dan `Level_109`, RTX 3050 Ti Laptop 4 GB, Core i7-11370H, Windows Balanced, dan charger terhubung. Direct3D 11, 1920×1080, preset High Fidelity, skala render internal 1,00, bayangan 75 m, VSync 0, batas internal 400 FPS. Setiap arah pandang diukur selama 15 detik di sekitar checkpoint Beach → Tropics. Satu pemain adalah P1 dalam room offline mod dengan satu kamera. Semua frame sampel diambil saat window berfokus.

| Pemain | FPS searah | Rata-rata / p99 searah (ms) | FPS beda arah | Rata-rata / p99 beda arah (ms) | VRAM proses maksimum (MiB) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 70,38 | 14,21 / 17,18 | 77,54 | 12,90 / 14,52 | 2343 |
| 2 | 47,86 | 20,89 / 23,38 | 71,66 | 13,95 / 17,37 | 2256 |
| 3 | 35,82 | 27,92 / 36,33 | 59,06 | 16,93 / 22,67 | 2301 |
| 4 | 29,11 | 34,36 / 43,57 | 42,20 | 23,70 / 30,84 | 2215 |

| Empat detik saat join | FPS rata-rata | Frame time rata-rata / p99 (ms) |
| --- | ---: | ---: |
| P2 | 46,47 | 21,52 / 63,80 |
| P3 | 43,41 | 23,04 / 71,17 |
| P4 | 38,72 | 25,83 / 85,60 |

Dua pemain melewati rata-rata 60 FPS pada sampel beda arah saja. p99 17,37 ms masih di atas anggaran 16,67 ms. Pengukuran ini belum membuktikan 60 FPS yang stabil pada konfigurasi split-screen. Waktu render internal GPU tidak tersedia dari Unity. VRAM adalah puncak penghitung dedicated GPU memory Windows untuk proses PEAK pada dua jendela sampel. Karena peta berbeda dari pengukuran historis, angka ini tidak mengukur perubahan FPS antar-rilis.

[Sampel frame](performance/v0.1.11-dx11.frames.csv) · [Sampel VRAM](performance/v0.1.11-dx11.gpu.csv)

## Benchmark historis dua pemain versi 0.1.6

Uji di RTX 3050 Ti Laptop, Core i7-11370H, Windows Balanced dan game PEAK 3.1.0 build 26542162. Dua panel 1920×540 menghasilkan output 1920×1080. Kedua API memakai High Fidelity, render scale 1,00, jarak bayangan 75 m, VSync 0 dan batas FPS internal 400. Gamepad uji berasal dari Unity Input System. Semua jendela sampel memiliki fokus dan setiap skenario diukur selama 15 detik pada checkpoint Beach ke Tropics di Level_108.

| Arah kamera | Direct3D 12 FPS | Direct3D 11 FPS | Direct3D 12 rata-rata / p99 | Direct3D 11 rata-rata / p99 |
| --- | ---: | ---: | ---: | ---: |
| Searah ke area vegetasi dan kabut | 29,33 | 53,81 | 34,09 / 38,51 ms | 18,58 / 21,85 ms |
| Berbeda arah | 51,17 | 89,02 | 19,54 / 27,59 ms | 11,23 / 15,02 ms |

Peningkatan pada sampel ini sekitar 83% dan 74%. Angka searah masih di bawah 60 FPS. Kedua API memakai dua kamera, ukuran viewport dan pengaturan grafis yang sama. Dalam cuplikan camp keduanya menampilkan vegetasi, bayangan, kabut, air terjun, api dan HUD. Posisi scout, animasi serta partikel berubah antar peluncuran, sehingga tangkapan layar bukan uji kesamaan setiap piksel. Hasil juga belum mencakup semua biome atau tiga sampai empat pemain di Direct3D 11.

Unity melaporkan Direct3D 12 dengan NativeGraphicsJobsSplitThreading, dan Direct3D 11 dengan LegacyJobified. NVIDIA melaporkan SW Thermal Slowdown ketika GPU berada di sekitar 87°C pada seri uji tambahan. Karena sesi dilakukan berurutan dan kondisi termal dapat berubah, hasil membandingkan sesi yang terukur dan belum menetapkan FPS minimum untuk seluruh run.

## Percobaan lain

- Mengaktifkan GPU occlusion pada Direct3D 12 membuat kondisi searah sekitar 28 FPS, dibanding 30–32 FPS pada sampel pembanding. Pengaturan GPU occlusion normal dipakai pada rilis.
- Menonaktifkan occlusion bawaan per kamera menghasilkan 28,57 FPS sementara sampel kontrol terdekat 28,19 FPS. Selisihnya kecil dan konfigurasi kamera asli dipertahankan.
- Perbandingan ABBA di Direct3D 12 mencakup jalur input, pemberitahuan HUD, dan recorder tamu. Gabungan sampel searah 29,99 FPS untuk jalur lama dan 29,77 FPS untuk jalur baru. Pada arah berbeda tercatat 51,22 dan 53,81 FPS. Dampaknya lebih kecil daripada perubahan API.
- Saat kamera P2 sementara dimatikan untuk diagnosis, FPS arah berbeda naik dari 51,06 menjadi 72,30 dengan dua scout masih berada di simulasi. Rilis tetap merender kedua panel.

Sampel frame per skenario tersedia di [Direct3D 11](performance/dx11.frames.csv), [Direct3D 12](performance/dx12.frames.csv), [uji occlusion GPU](performance/audit.frames.csv), dan [uji ABBA](performance/cpu.frames.csv). Sampel NVIDIA mencakup keadaan GPU sistem selama uji tambahan. [Telemetri](performance/cpu.gpu.csv)

Cuplikan pada arah searah tersedia untuk [Direct3D 12](../preview/benchmark-dx12.png) dan [Direct3D 11](../preview/benchmark-dx11.png). Unity menyediakan flag pemilihan API per proses. [Referensi parameter Unity Player](https://docs.unity3d.com/Manual/PlayerCommandLineArguments.html)

Uji launcher melalui Steam mengonfirmasi Direct3D 11, lobi otomatis dengan tombol mode biasa tersembunyi, dua pemain dalam Airport offline, gerak dan lihat P2 terpisah, serta penutupan proses ketika room ditinggalkan. Peluncuran Steam standar melewati jalur ini karena flag khusus tidak ada.
