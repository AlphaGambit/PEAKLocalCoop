# Hasil verifikasi dan performa

Pengujian dilakukan 21 November 2026 pada PEAK 3.1.0, Steam build 26542162, lewat satu proses PEAK Windows dengan BepInEx 5.4.23.3. Input controller probe berasal dari perangkat Unity Input System sintetis. Launcher tetap memakai Steam biasa. Semua hit, inventory, checkpoint dan scene di bawah berjalan di game terpasang.

## Regresi pemberian item 0.1.12

Probe `-peak-coop-feed-check` berjalan pada PEAK 3.1.0 build 26542162 di pulau. Empat kasus lulus melalui RPC pemberian asli PEAK, yaitu bandage dan Apple Berry pada kedua arah P1↔P2. Item terpakai hilang dari tangan dan slot pemberi, dan jumlah item penerima tetap sama. Probe memanggil aksi pemberian secara langsung setelah pickup dan equip, sehingga fokusnya jalur konsumsi dan inventory.

## Regresi dunia 0.1.11

`-peak-coop-world-check` dijalankan pada 22 November 2026 di PEAK 3.1.0 Steam build 26542162, Direct3D 11, `Level_109`. Fixture menyiapkan kondisi uji dalam dunia yang sama dan memanggil jalur PEAK yang terpasang.

| Skenario | Hasil |
| --- | --- |
| Spike roller dan kapak | Kedua callback asli menerima objek Collision terkontrol dengan collider dari karakter yang benar. P1 dan P2 masing-masing menerima +0,15 cedera dalam setiap skenario. Cooldown P1 tidak memblokir cedera P2. |
| Kulit pisang | P2 memenuhi syarat jarak, kecepatan dan grounded. RPC native membuat P2 terpeleset. P1 tetap seperti semula. |
| Flag personal | Prefab flag diinisialisasi untuk P2, lalu jalur `TryCheckpoint` dan RPC `RegainItems` memulihkan tepat satu item ke P2. Inventory P1 tetap kosong. Pemilihan kamera preview dan konsumsi slot ketika memasang flag ditelusuri dari IL. Pemasangan memakai controller masih perlu diuji. |
| Marker | RPC native membuat satu marker. Callback kamera mencatat perbedaan orientasi 58,44° antara panel P1 dan P2. Skala keduanya sama pada jarak fixture ini. Perhitungan skala memakai jarak scout pada panel yang dirender. |

Run sebelumnya di Level_108 mencapai 4 pemain dan akhir run bersama. Pada run panjang di Level_109, crosshair P2 menemukan item tetapi pickup dari input West gagal, sehingga probe dihentikan sebelum checkpoint. Probe dunia kemudian memeriksa perbaikan di atas tanpa bergantung pada pickup. Uji emote, bidik fixture ping dan perpindahan fisik P4 juga tidak konsisten di salah satu run panjang. Hasil ini tidak menetapkan bahwa semua kontrol, interaksi, biome atau perangkat fisik telah lulus.

## Regresi cedera dan hantu 0.1.10

Pengujian dilakukan di PEAK 3.1.0 Steam build 26542162 pada `Level_108`. Controller berasal dari Unity Input System dan seluruh karakter memakai prefab PEAK. Fixture gergaji memanggil callback benturan asli dengan collider karakter yang terdaftar. Pemain berada dalam satu sesi empat kursi setelah checkpoint.

| Skenario | Bukti runtime |
| --- | --- |
| Cedera dan cooldown | Callback gergaji memberi +0,20 cedera kepada P1 dan P2. Benturan P1 tidak menghabiskan cooldown P2. Panggilan berulang pada P2 selama cooldown tidak memberi cedera tambahan. |
| Pingsan | Cedera membawa total status P2 ke 1,00 dan memicu pingsan lewat `Character.HandleLife`. P1 tetap sadar. Bar timer tampil di HUD P2, dan hantu belum aktif. Jalur menggendong P2 juga bekerja. |
| Mati | Input interaksi P2 ditahan setelah dilepas dari gendongan. Timer native mencapai 1,00, P2 mati, dan P1 tetap hidup. |
| Pemilik dan target hantu | Prefab hantu P2 muncul dengan pemilik P2, memakai target hidup, dan berganti target bersama kursi P2. Hantu P1 juga muncul ketika P1 mati sementara pemain lain masih hidup. |
| Visibilitas per kamera | Callback URP memeriksa bahwa renderer hantu P2 aktif pada panel P1 dan disembunyikan pada panel P2. Hantu P1 aktif pada panel P2 dan disembunyikan pada panel P1. |
| Revive dan akhir run | Jalur revive native menghapus hantu serta mengembalikan pemain hidup. Ketika semua scout mati, game menampilkan satu layar akhir. |
| HUD saat hidup | Probe mengukur reticle Airport di pusat viewport P1 dan P2. Slot P3–P4 terdeteksi di panel masing-masing. |

Jalur spike roller dan pemulihan inventory lewat flag personal diperbaiki dari inspeksi simbol. Demo ini menguji callback gergaji dan revive native tanpa menjalankan peta perangkap lengkap atau memicu item flag personal. Penerimaan manual dengan controller fisik masih terpisah.

## Regresi kontrol 0.1.9

Diagnostik `-peak-coop-logic-check` memakai satu proses PEAK 3.1.0 pada 1920×1080. PEAK menerima event gamepad sintetis lewat Unity Input System. Pengujian ini membuktikan alur input dan gameplay di build terpasang. Pengujian perangkat fisik melalui Steam Input dan remap driver masih terpisah.

| Pemeriksaan | Bukti yang dicatat |
| --- | --- |
| Reticle | Di Airport, dot P1 dan P2 masing-masing berukuran 5 × 5 unit UI di tengah viewport. Pada dinding pulau, P2 beralih dari Reticle ke ReticleClimb, sementara HUD P1 tetap. |
| Ping | R3 P3 menempatkan marker asli pada target uji yang berada di arah kamera P3. Host tidak mengirim marker dari input itu. |
| D-pad dan emote | Kanan dan kiri memindahkan hotbar P3, bawah melepas pilihan, dan atas membuka roda P3. Stik kanan memilih slice. Lepas atas memainkan emote hanya pada P3. |
| Penonton | Setelah P2 mati, D-pad memilih scout yang lain. LB mengubah jarak kamera P2 dari 2,50 ke 7,66 unit, lalu RB mendekatkannya ke 2,21 unit. Revive mengembalikan gameplay. |
| Rope Spool | Probe menahan RB, lalu langsung menahan LB. Pemeriksaan memakai waktu fixed step agar durasi simulasi tidak memendek pada FPS rendah. Panjang tercatat 3,0 → 8,1 → 3,5 segmen native. Arah scroll tercatat +0,40 lalu −0,40, indikator muncul di HUD P3, dan input scroll P1 tetap kosong. |

Perbaikan mencakup reticle, arah ping dan pembuatan marker. Visual marker dunia masih menyesuaikan ukuran dan orientasi dengan kamera utama. Belum ada uji end-to-end binokular, seluruh hazard, atau kondisi kemenangan summit. Lihat [audit simbol](AUDIT.md).

## Regresi Local Co-op 0.1.8

Uji `-peak-coop-logic-check` memakai build 26542162 pada `Level_108`. Perangkat controller berasal dari Unity Input System. Posisi dan beberapa kondisi awal disiapkan oleh diagnostik, sementara pickup, drop, kamera, efek status, checkpoint, revive dan akhir run memakai jalur PEAK yang terpasang.

| Skenario | Pemeriksaan |
| --- | --- |
| Aim dan item | Hit dan proyektil Rope Cannon P2 memakai sasaran P2. Rescue Hook P3 mengembalikan ray yang sama dengan pusat viewport P3. |
| Jetpack | Roda jetpack P4 menyediakan entri bahan bakar. Setelah mesin dijalankan lewat logika gerakan PEAK, nilai bahan bakar P4 berkurang dan perlengkapan P1 tetap. |
| Ransel | P2 membuka roda dengan controller. Saat tas dipindah 18 unit, roda P2 tertutup. P1 tidak membuka roda. |
| Efek status | Efek stamina dalam radius kecil di sekitar P2 hanya mengenai P2. RPC terarah ke P2 juga hanya mengenai P2 walaupun radius mencakup P1. Sampel volume lava sintetis menambah panas P2 yang berada di dalam volume dan mempertahankan status P1 yang jauh. |
| Inventory dan input | Input dan arah pandang P3–P4 terpisah. Setiap kursi mengambil item melalui inventory native dan melepas satu item lewat controller. Gerak P3/P4 tercatat 1,10/1,08 m dengan perubahan arah pandang 58,15°/58,85°. Inventory kursi lain tetap. Makanan rusak yang dipegang P3 mengosongkan slot P3 dan jumlah item P1 tetap. Uji P2 juga mencakup pickup serentak dan sambung ulang. |
| Kelanjutan run | Kiosk memulai transisi Airport ke pulau. Campfire menaikkan segmen Beach ke Tropics. P3–P4 bergabung dengan inventory kosong dan progres sebelumnya tetap. Pengujian dilanjutkan dengan menggendong, satu scout mati, revive, penutupan gate dan akhir run bersama setelah seluruh scout mati. |

Belum diuji secara langsung pada 0.1.8 mencakup overlay binokular, seluruh variasi item khusus dan hazard, serta kemenangan dan sinematik summit. Bukti inspeksi dan simbol yang masih bergantung pada P1 ada di [audit](AUDIT.md). Pengukuran performa pada versi terdahulu dipertahankan sebagai riwayat. Tidak ada benchmark FPS baru pada putaran ini.

## Rope Cannon versi 0.1.7

Probe menyiapkan dua permukaan terpisah di pulau dengan sudut kamera P1 dan P2 sekitar 110°. Setelah P2 memegang Rope Cannon, `WillAttach` memilih permukaan P2. Pemanggilan penyelesaian cast asli kemudian menghasilkan `RopeAnchorProjectile` dengan titik tujuan yang cocok dengan preview P2 dalam toleransi 0,05 unit. Jalur raycast item lain diaudit dan memakai adapter pemegang yang sama. Belum semua item dan biome diuji secara terpisah.

## Alur yang diperiksa

| Skenario | Bukti runtime |
| --- | --- |
| Dua karakter dan inventory terpisah | Probe Airport menemukan actor 1 dan 2 dengan view 10002 dan 20008. Selama input pad P2, P2 berpindah 0,65 m dan mengubah yaw 62,76°. P1 berpindah 0,01 m dengan yaw tetap. Pickup native memberi item kepada P2. |
| Input P2–P4 | Dalam probe benchmark berikutnya, tiap pad menggerakkan dan mengubah arah pandang kursinya sendiri (P2 1,09 m dan 58,03°, P3 1,07 m dan 59,58°, P4 1,14 m dan 60,05°), sementara input gerak dan arah pandang kursi lain tetap. Menekan Start lagi pada pad yang sudah dipakai tidak mengisi slot lain. |
| Lobby dan run | Input Start dari pad sintetis mengisi P2 di menu. Kiosk Airport native membuka `Level_108` dalam room offline yang sama. Kursi dan identitas GUID tetap. Prefab di pulau memiliki view terpisah. |
| Pickup, persaingan dan drop | Sinar dari kamera P2 mengarah pada Airplane Food. Input West mengambilnya ke inventory P2. Upaya dua peserta di frame yang sama menambahkan satu item ke satu inventory. Tombol North mengosongkan slot P2 dan menambah satu item ground. |
| Mendaki dan ransel | P2 memakai trigger kanan pada dinding yang dekat dengan kamera P2. Status native `isClimbing` berubah hanya pada P2. Pada uji ransel, West membuka panel P2 sementara `usingBackpackWheel` P1 tetap false. |
| Controller terputus | Ketika pad P2 dilepas, objek Character P2 dan inventory tetap. Start pada pad sintetis pengganti mengikat perangkat baru ke P2. |
| Campfire dan layout | Setelah campfire Beach dinyalakan dengan jalur native, segmen naik ke Tropics. P3 dan P4 bergabung dari Start pada controller bebas. Mereka muncul dengan stamina 1,00 dan inventory kosong, sementara inventory P2 dan segmen Tropics tetap. Setelah host meninggalkan radius, kembali ke api yang sama tidak membuka gate lagi. |
| Kamera dan audio | Screenshot 1920×1080 menunjukkan dua panel 1920×540, tiga panel dengan P1 1920×540 di atas dan dua panel 960×540 di bawah, serta grid empat panel 960×540. HUD P1 mengikuti viewport dan HUD tamu terpisah. Probe menghitung satu AudioListener aktif di Airport dan pulau. |
| Membantu, kematian dan revive | P1 menggendong P2 yang pingsan melalui `CharacterInteractible.Interact` native. Kematian P2 saat anggota lain masih hidup membiarkan run berjalan. RPC revive native mengembalikan P2. Saat semua scout mati, layar akhir terbuka. Jalur kemenangan/sinematik summit masih perlu diuji. |

Belum ada sesi manual dengan empat controller fisik, termasuk emulasi Steam Input, perangkat yang memberi dua entri, rebind khusus dan hotplug USB. Jalur penyebab damage di setiap biome, item khusus, semua jenis ransel dan semua peta harian perlu pengujian tambahan. Kasus campfire yang diuji menggunakan warp diagnostik, api dan transisi segmen native. Probe menilai integrasi, sementara skenario pendakian lengkap dari pantai ke summit masih memerlukan sesi manual.

## Benchmark 1080p versi 0.1.0

Laptop memakai RTX 3050 Ti Laptop GPU, 3962 MB yang dilaporkan Unity, Intel Core i7-11370H generasi ke-11, RAM 15,7 GiB, charger terhubung dan Windows **Balanced**. Renderer Direct3D 12, URP **High Fidelity**, render scale **1,00**, jarak bayangan **75 m**, VSync 0, batas internal game 400 FPS. Output 1920×1080. Seluruh frame uji dibaca saat window PEAK berfokus.

Benchmark memakai `Level_108`, di campfire yang memajukan Beach ke Tropics. Angka satu pemain memakai gameplay solo aktual dengan instrumentasi mod aktif dan satu kamera stock. Pemain kemudian bergabung bertahap ke dunia dan camp yang sama. Jarak antar karakter pendek. Untuk kondisi searah dan arah berbeda, masing-masing disampel 15 detik sesudah mengatur arah pandang. Jalur searah melihat vegetasi, gunung dan kabut di sekitar camp. Ini merupakan tampilan terberat yang terukur pada benchmark ini. Peta atau biome lain belum disampling.

| Pemain | Searah FPS | Searah rata-rata / p99 ms | Arah berbeda FPS | Arah berbeda rata-rata / p99 ms | Dedicated GPU memory puncak (MiB) |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | 53,88 | 18,56 / 21,88 | 58,98 | 16,95 / 20,16 | 2.330 |
| 2 | 31,41 | 31,84 / 35,08 | 54,28 | 18,42 / 23,94 | 2.224 |
| 3 | 21,02 | 47,57 / 53,63 | 43,13 | 23,19 / 27,28 | 2.269 |
| 4 | 15,12 | 66,15 / 73,09 | 25,04 | 39,93 / 50,02 | 2.181 |

| Setelah join | FPS selama 4 detik | Rata-rata / p99 ms |
| --- | ---: | ---: |
| 2 pemain | 32,34 | 30,93 / 49,82 |
| 3 pemain | 27,38 | 36,53 / 89,35 |
| 4 pemain | 22,51 | 44,43 / 138,86 |

FPS rata-rata adalah 1000 dibagi rata-rata frame time. p99 berarti 99% frame lebih cepat atau sama dengan waktu tersebut. Angka dedicated GPU memory per kursi adalah puncak sampel Windows untuk proses PEAK selama dua arah kamera, dijumlahkan lintas adapter. FrameTimingManager tidak menyediakan GPU frame time dalam run ini.

### Perbandingan grafis dengan empat pemain

Pengukuran berikut berasal dari sesi verifikasi terpisah di camp yang sama, dengan empat pandangan searah dan jendela sampel 12 detik. Uji berikutnya memberi urutan yang sama dengan 19,38 / 16,11 / 16,64 FPS. Setting dikembalikan setelah probe. Windows mengukur dedicated GPU memory proses per skenario.

| Kondisi | Output | Skala internal URP | Bayangan | FPS | Rata-rata / p99 ms | Dedicated GPU memory puncak |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Referensi | 1920×1080 | 1,00 | 75 m | 19,13 | 52,28 / 67,72 | 2.216 MiB |
| Jarak bayangan separuh | 1920×1080 | 1,00 | 37,5 m | 15,41 | 64,88 / 86,72 | 2.180 MiB |
| Skala tiga perempat | 1920×1080 | 0,75 | 75 m | 16,40 | 60,96 / 72,35 | 2.155 MiB |

Skala 0,75 memberi ukuran internal nominal ekuivalen 1440×810 untuk output, atau 720×405 per panel grid sebelum upscale. Render scale dan bayangan yang lebih rendah tidak memberi peningkatan FPS pada sampel ini. Perbedaan arah dan jumlah kamera sangat mengubah frame time. Ini konsisten dengan beban yang berubah per kamera di adegan, sementara data yang ada belum memisahkan submission CPU, render pass GPU dan perubahan beban sepanjang run. Tidak ada perubahan default grafis. **Target 60 FPS belum tercapai pada kondisi uji di atas.**
