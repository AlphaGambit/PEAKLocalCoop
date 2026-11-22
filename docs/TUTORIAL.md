# Tutorial PEAK Local Co-op

Mainkan PEAK berdua sampai berempat di satu PC dan satu layar. P1 memakai keyboard dan mouse. Sediakan satu controller untuk setiap pemain tambahan.

## 1. Pasang mod di Windows

1. Pastikan PEAK versi **3.1.0**, Steam build **26542162**, sudah terpasang. Untuk melihat Build ID, buka **Steam Library → PEAK → Properties → Updates**.
2. Unduh **PEAKLocalCoop-0.1.11.zip** dari bagian **Assets** pada [rilis v0.1.11](https://github.com/AlphaGambit/PEAKLocalCoop/releases/tag/v0.1.11).
3. Klik kanan ZIP, pilih **Extract All / Ekstrak Semua**, lalu simpan hasilnya di luar folder Steam, misalnya di Documents.
4. Tutup PEAK. Buka folder hasil ekstrak dan jalankan **Install or Repair Local Co-op.cmd**. Pada pemasangan pertama, siapkan koneksi internet agar pemasang bisa mengambil BepInEx dari Thunderstore.
5. Tunggu pesan **PEAK Local Co-op 0.1.11 siap**, lalu sambungkan controller dan biarkan Steam terbuka.

## 2. Mulai pendakian

1. Jalankan **Play Local Co-op DX11.cmd** dari folder hasil ekstrak. PEAK langsung membuka lobi **Local Co-op**. Jika membuka game lewat Steam, pilih **Local Co-op** di menu utama, di bawah **Play Offline**.
2. P1 langsung memakai keyboard dan mouse. Pemain kedua dan seterusnya menekan **Start/Menu** pada controller masing-masing. Pastikan kursi pemain yang ikut sudah mendapat tanda centang.
3. P1 klik **Masuk Airport**. Di Airport, gunakan kiosk dan boarding pass seperti biasa untuk memulai pendakian.
4. Setiap pemain memakai karakter, kamera, stamina dan inventory di panelnya sendiri. Dua pemain ditata atas-bawah. Dengan tiga pemain, P1 tetap di atas dan dua pemain lain berbagi baris bawah. Empat pemain memakai grid 2×2.

## 3. Kontrol controller

| Aksi | Tombol |
| --- | --- |
| Pindah slot inventory cepat | D-pad kiri/kanan |
| Kosongkan tangan atau kembali ke slot terakhir | D-pad bawah |
| Pilih barang di roda ransel | D-pad untuk memilih, tombol bawah untuk konfirmasi, tombol kanan untuk tutup |
| Ulur dan gulung Rope Spool yang dipegang | Tahan RB/R1 untuk mengulur dan LB/L1 untuk menggulung |
| Pasang Rope Spool | Bidik permukaan dan tahan RT/R2 |
| Emote | Tahan D-pad atas, pilih dengan stik kanan, lalu lepas D-pad atas |
| Ping | Tekan stik kanan (R3). P1 bisa memakai klik tengah mouse. |
| Pilih scout untuk ditonton saat mati/pingsan | D-pad kiri/kanan. RB/R1 dan LB/L1 mengatur jarak kamera. |

## 4. Tambah pemain atau sambungkan kembali controller

- Untuk menambah pemain saat run, nyalakan campfire yang memajukan pendakian, lalu tunggu pemberitahuan checkpoint. Selama party masih di area checkpoint dan ada kursi kosong, tekan **Start/Menu** pada controller yang belum dipakai.
- Controller lama bisa disambungkan kembali kapan saja. Untuk satu kursi yang terputus, tekan **Start/Menu** pada controller pengganti. Jika beberapa kursi putus sekaligus, P1 memilih kursi dengan **F6** untuk P2, **F7** untuk P3, atau **F8** untuk P4, lalu pemain menekan **Start/Menu** pada controller pengganti.

## 5. Pemulihan dan pencopotan

- Jika file mod hilang setelah Steam memperbarui atau memverifikasi file game, jalankan **Play Local Co-op DX11.cmd** untuk memulihkannya. **Install or Repair Local Co-op.cmd** juga bisa dipakai untuk memulihkan file tanpa membuka game.
- Jika launcher melaporkan **File PEAK berubah**, cek halaman Releases untuk versi mod yang mendukung build PEAK baru. Untuk sementara, buka PEAK dari Steam untuk bermain biasa.
- Jika masalah berlanjut, cek **BepInEx/LogOutput.log** di folder instalasi PEAK. Sertakan build PEAK, langkah memicu masalah, dan pesan error yang relevan saat melapor.
- Untuk melepas mod, tutup PEAK dan hapus folder **BepInEx/plugins/PeakLocalCoop** di instalasi PEAK. Hindari menjalankan launcher mod lagi karena launcher akan memulihkan DLL. Hapus folder ekstrak ZIP dan **%LOCALAPPDATA%\PEAKLocalCoop** jika juga ingin menghapus salinan pemulihan.

Untuk instalasi lewat mod manager, build dari source, atau catatan kompatibilitas, baca [README](../README.md).
