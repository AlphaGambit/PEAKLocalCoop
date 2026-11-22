# Integrasi dan pembaruan

## Batas adapter

| File | Titik integrasi dan alasan |
| --- | --- |
| `src/Peak/BuildGuard.cs` | Periksa Unity, SHA-256 assembly yang relevan dan `globalgamemanagers`. Pemasangan hook dibatalkan bila fingerprint berbeda atau lookup simbol gagal. Log menyebut file, hash atau simbol yang perlu diperiksa. |
| `src/Peak/OfflineScouts.cs` | Pakai `Room.StorePlayer`, prefab pool PUN dan `PhotonNetwork.AllocateViewID`. Asosiasikan `Player` dan `Character` melalui `PlayerHandler`. Jalankan Awake sebagai actor tamu agar host tetap mengisi singleton. Sesudah Awake, tetapkan controller view ke actor host agar logika lokal berjalan. Batalkan actor, view, model, inventory dan registrasi jika spawn gagal. |
| `src/Peak/SeatActions.cs`, `SeatHooks.cs` | Gunakan definisi dan override binding PEAK untuk masing-masing kursi. `CharacterInput.Sample`, hotbar dan `CharacterMovement.CameraLook` mengganti referensi input hanya selama panggilan native dan memulihkannya lewat finalizer. `UIInputHandler.Sample` dan `HostMenuInput` memfilter menu utama sesi ke keyboard dan mouse P1. `Character.IsLocal` mengaktifkan gameplay asli untuk tamu room offline. RPC dengan target actor tamu menjalankan callback di proses host. |
| `src/Peak/SeatInteraction.cs` | Setiap tamu memiliki `Interaction` dengan hover dan timer hold sendiri. Saat tick, arahkan lookup karakter dan inventory native ke tamu selama panggilan sinkron. Alihkan ray dari `MainCamera.instance` ke kamera kursi untuk interaksi, `CharacterClimbing.TryToStartWallClimb` dan preview piton. Item ground yang sudah dinonaktifkan tidak menerima permintaan pickup berikutnya di frame yang sama. |
| `src/Peak/GuestInventory.cs` | Tangkap `GUIManager.OpenBackpackWheel` dalam konteks interaksi tamu. Gunakan data slot dan operasi Wear/Stash/Interact dari PEAK lewat UI per kursi. Inventory dan HUD P1 memakai jalur PEAK. |
| `src/Peak/PartyRuntime.cs` | Buka alur solo native dan ikuti room yang sama selama Airport dan pulau. Saat load scene, lepas pegangan yang terkait scene lama lalu pasang ulang kursi pada prefab di scene baru. Kandidat checkpoint dan viewport hanya dibuat pada room lokal. P1 menentukan perpindahan level. |
| `src/Peak/CheckpointGate.cs` | Tangkap `Campfire.Light_Rpc` ketika `updateSegment` bernilai true. Ingat camp yang menyalakan transisi, yang menjadi `MapHandler.PreviousCampfire` setelah segmen maju. Syaratnya api asli menyala, `EveryoneInRange()` true, host hidup, pesan scene tidak sedang tertahan dan tidak ada lava aktif pada Kiln. Titik spawn harus mempunyai tanah yang cukup datar, bebas collider, dan punya jarak aman dari anggota party. |
| `src/Peak/GuestHud.cs` | Ambil cabang `GUIManager.bar.transform.parent` (BarGroup), layout `GUIManager.items` dan `GUIManager.backpackWheel` saat scene aktif. Render salinannya pada canvas yang mengikuti viewport kursi dan tinggi referensi 1080 unit milik HUD asli. `StaminaBar.Update`, `BarAffliction` dan `InventoryItemUI.SetItem/SetSelected` membaca karakter tamu selama pembaruan sinkron. Pulihkan konteks di akhir panggilan. Pilihan D-pad menyorot slice pada roda asli. Saat update game, periksa juga urutan sibling slot, `InputIcon.SetText`, `Character.MoraleBoost`, serta callback stamina tak terbatas. |
| `src/Peak/SplitViews.cs` | Pertahankan satu audio listener di kamera asli. Buat kamera URP base menggunakan setting kamera utama. `Camera.rect` menentukan viewport. Alihkan `Canvas_HUD` P1 ke Screen Space Camera untuk mengikuti panel. Tamu punya reticle, label, status dan inventory dalam panel sendiri. |
| `src/Peak/SharedWorldHooks.cs` | Teruskan kabut, angin dan buff campfire yang berpusat pada karakter lokal ke karakter tamu yang ada dalam area. Pakai jalur shared end game PEAK lalu tandai akhir run pada setiap kursi. |
| `src/Peak/TitleCoopEntry.cs` | Cari `MainMenuMainPage.m_soloButton` dan `m_settingsButton` pada canvas Title. Tombol solo tampil sebagai Play Offline pada menu berbahasa Inggris. Tambahkan tombol uGUI di antaranya. Buat ruang pada baris di atas dan di bawah karena build ini menempatkan tombol manual. Sambungkan navigasi keyboard/controller dan pulihkan posisi serta navigasi saat adapter dilepas. |
| `src/Local/` | Aturan slot, identitas, pemilihan perangkat, UI dan layout. `TrailLobby` memakai canvas dan font runtime PEAK, dengan palet dan kartu kursi milik mod. Empat PNG original untuk lencana, papan kayu dan ikon perangkat disematkan dalam DLL. Lobi mendaftarkan kontainer navigasi PEAK agar input pilih dan konfirmasi berada di lobi. |

Build ini mempunyai dua jenis ID di adapter. GUID kursi tetap sama saat pindah scene atau ganti perangkat. ActorNumber dan ViewID hanya mewakili objek dalam room yang sedang dipakai. Nama `Local P2` dan seterusnya adalah label tampilan. Desain fase berikutnya perlu membuktikan model actor yang didukung Photon sebelum memakai format nama tamu Steam.

Untuk registrasi perangkat, satu ID perangkat hanya bisa menempati satu kursi. Serial yang tersedia dianggap sebagai identitas perangkat tambahan dan permintaan join sangat berdekatan diberi jeda 400 ms. Dua entri virtual tanpa identitas perangkat yang sama bisa tetap tampak terpisah. Satu controller fisik dengan dua entri berbeda perlu diuji menurut konfigurasi Steam Input dan driver yang dipakai.

### Perangkap, flag dan marker pada 0.1.11

| Simbol | Integrasi |
| --- | --- |
| `SwingingAxe.OnCollisionEnter` dan helper `BonkScout` | Ganti tujuan P1 dengan karakter dari collider yang sudah divalidasi. Cooldown tamu memakai mekanisme per karakter yang sama seperti gergaji dan spike roller. |
| `BananaPeel.Update` dan `RPCA_TriggerBanana` | Saat timer native siap, pilih scout hidup terdekat yang berada dalam radius, menyentuh tanah, dan bergerak cukup cepat. Semua pembacaan pada frame itu memakai calon yang sama. Timer, gaya dan RPC slip tetap native. |
| `Constructable.Update/TryUpdatePreview/CreateOrMovePreview/FinishConstruction` | Preview membaca kamera pemegang. Pemasangan mengosongkan slot pemegang. `CheckpointFlag.Initialize` menyimpan status dan owner native yang digunakan ketika `Character.TryCheckpoint` memulihkan inventory. |
| `PointPing.Start/Go` dan callback render URP | Posisi marker dan RPC tetap di dunia bersama. Saat kamera kursi dirender, rumus PEAK membaca kamera dan scout dari panel itu untuk skala dan orientasi. Transform dipulihkan setelahnya. |

### Pingsan, hantu dan cedera pada 0.1.10

| Simbol | Implementasi |
| --- | --- |
| `Character.HandleLife`, `UpdateVariablesFixed`, `HandlePassedOut` | PEAK tetap menentukan ambang pingsan, akumulasi timer, kemungkinan pulih atau checkpoint, dan perubahan menjadi mati. Adapter tidak mengganti status invincibility atau besar damage. |
| `GUIManager.UpdateDyingBar` | Salin bar PEAK ke panel tamu, termasuk gradien, indikator flag, indikator jamur dan animasi kematian. Isian membaca `1 - deathTimer` dari karakter di panel. |
| `MainCameraMovement.Spectate`, `PlayerGhost.RPCA_InitGhost/RPCA_SetTarget` | Saat scout tamu sudah mati dan menonton scout hidup, buat satu prefab PEAK, isi view pemilik dan target melalui RPC asli. Perbarui target menurut kursi. Hapus view hantu ketika pemilik hidup lagi, saat sesi berakhir atau saat scene dibongkar. |
| `PlayerGhost.RPCA_InitGhost`, callback kamera URP | Native menyembunyikan renderer pemilik secara global bila view dimiliki proses lokal. Dalam room co-op, terapkan penyembunyian hanya saat kamera pemilik dirender dan pulihkan setelahnya. Kamera pemain lain memakai renderer asli. |
| `MovingSawBlade.OnTriggerStay`, `SpikeRoller.OnCollisionEnter` | Cocokkan karakter yang sudah divalidasi dari collider. Waktu hit per tamu dipisah dari cooldown P1. Damage native, gaya dan pengecualian status tetap memakai jalur PEAK. |
| `Character.TryCheckpoint` | Dua lookup P1 untuk RPC `RegainItems` dialihkan ke pemakai checkpoint. Berlaku untuk checkpoint flag personal, terpisah dari join di campfire. |

### Pointer dan input pada 0.1.9

| Simbol | Bukti dan adapter |
| --- | --- |
| `PointPinger.DoPing`, `TryGetPingHit`, `ReceivePoint_Rpc` | Jalur native menghitung posisi layar global dan memanggil helper raycast statis dengan `Camera.main`. `SeatPing` menyimpan kamera pengirim selama `DoPing`, lalu membidik dari pusat viewport. Simulasi cooldown, filter collider, dan pembuatan marker RPC tetap native. Pemeriksaan jarak saat marker dibuat menggunakan karakter pengirim tamu, sehingga kedekatan P1 tidak menjadi syarat dalam satu proses. |
| `GUIManager.UpdateReticle`, `SetReticle` | Reticle dan indikator lempar asli memiliki state per tamu. Pada roda emote dan ransel, panel tamu menutup reticle dan teks interaksi. |
| `CharacterInput.Sample`, `ResetInput`, `RopeSpool.Update/FixedUpdate` | RB dan LB adalah `ScrollForward` dan `ScrollBackward`. `Sample` juga dapat menghapus keduanya saat kombo `PushToTalk` terdeteksi. Action voice chat dimatikan pada salinan gamepad co-op. Saat sampel gameplay diblokir, field scroll dan ping yang tidak di-reset PEAK dikosongkan untuk mencegah aksi yang tertinggal. Pada spool tamu yang aktif, adapter membaca kedua action bumper dari perangkat pemegang dan menghentikan momentum lama saat arah berubah. PEAK tetap menentukan batas panjang, segmen dan penempatan anchor. Indikator `UI_Rope` memakai jumlah segmen spool yang dipegang. |
| `CharacterItems.DoSwitching`, `CharacterInput.action_selectBackpack` | D-pad hotbar tetap membaca action native per kursi. Toggle ransel memakai action langsung dari perangkat kursi agar tombol bisa menutup roda walau `Sample(false)` tidak mengisi kembali field gameplay. |
| `GUIManager.UpdateEmoteWheel`, `EmoteWheel.InitWheel/Tab`, `CharacterAnimations.PlayEmote` | Roda asli disalin per panel. Pilihan dari stik kanan dan tab dari D-pad berasal dari kursi yang membuka roda. Slice tidak mengambil fokus global. Lepas tombol menjalankan animasi melalui view karakter itu. |
| `MainCameraMovement.GetFov/Spectate/HandleSpecSelection` | Panel tamu membaca nilai dasar dan tambahan FOV native per karakter, memakai arah pandang karakter yang sedang ditonton, dan menyimpan target serta zoom penonton per kursi. |

### Audit kepemilikan pada PEAK 3.1.0

| Simbol | Integrasi Local Co-op |
| --- | --- |
| `RopeShooter.WillAttach`, `OnPrimaryFinishedCast`, `VineShooter.WillAttach` dan `OnPrimaryFinishedCast` | `ItemSeat` membaca pemegang item untuk raycast, posisi awal dan kondisi grounded. |
| `Item.CancelUsePrimary`, `CancelUseSecondary`, `ItemCooking.CookingExplodeImpl`, `Dynamite.Update` | Animasi, cedera, dan pengosongan slot mengikuti pemegang. Jalur pembatalan dapat membaca pemegang terakhir setelah item dilepas. |
| `Dynamite.TestLightWick` | Pemeriksaan kedekatan native juga berlaku pada tamu. Flag native menghentikan pemrosesan saat objek sudah aktif. |
| `RescueHook.GetMiddleScreenRay` | Arah pusat viewport pemegang. Panel P1 juga memakai koordinat viewport ketika sesi Local Co-op aktif. |
| `CharacterMovement.TryJetpack` | Bahan bakar dan data peralatan dibaca dari karakter yang menjalankan gerakan. Dua pembacaan field diverifikasi saat memasang adapter. |
| `BackpackWheel.Update`, `TryFuel`, `GuestInventory` | Roda tamu mengikuti jarak native 6 unit. Jetpack punya entri isi bahan bakar terpisah dari slot barang. |
| `ThornOnMe.IsInteractible`, `Bugfix.IsInteractible` | Posisi dan arah kamera diambil dari karakter pada argumen interaksi. |
| `PhotonView.RPC(string, Player, object[])`, `GameUtils.AddMassAffliction`, `Action_ApplyMassAffliction.TryAddAfflictionToLocalCharacter`, `InfiniteStamAmulet.TryAddAfflictionToLocalCharacter`, `RitualDaggerFeedBehavior.RPC_RitualDaggerBuff` | `SeatHooks` menjaga identitas tujuan selama pemanggilan RPC offline. `AreaEffects` menerapkan efek siaran per karakter dan menerapkan efek terarah pada satu penerima. Filter jangkauan dan pengecualian penerima bawaan tetap dipakai. |
| `Lava.Heat`, `Lava.DoEffects`, `HotSun.Update`, `InjurySphere.Update`, `PlayerMoveZone.AddForceToCharacter` | Terapkan logika karakter pada tamu yang memenuhi syarat. Interval panas lava dan matahari dimiliki masing-masing karakter. `Lava.TryCookItems` tetap dipanggil satu kali oleh dunia. |
| `SlipperyJellyfish.OnTriggerEnter` | Kirim view karakter dari collider yang sudah divalidasi native. |

Saat fingerprint berubah, periksa kembali sumber data karakter, argumen RPC, kondisi dan side effect setiap leaf method. Untuk ubur-ubur, pastikan local 0 masih merupakan karakter hasil validasi collider. Sesudah build ulang, jalankan `-peak-coop-logic-check` dan periksa skenario dunia yang tidak tercakup di probe. Lihat [hasil verifikasi](VERIFICATION.md).

## Setelah Steam memperbarui build

1. Tutup PEAK. Baca `version.txt`, `PEAK_Data/boot.config`, `appmanifest_3527290.acf` di folder steamapps, dan periksa file adapter dengan `Get-FileHash -Algorithm SHA256`.
2. Pakai ILSpy pada assembly instalasi baru. Bandingkan signature dan perilaku simbol di tabel, terutama saat Awake, pemetaan actor/controller, action statis, RPC pickup dan drop, pemeriksaan campfire, serta shutdown pengguna. Periksa GraphicsSettings dan QualitySettings yang baru dan cocokkan dengan log runtime.
3. Sesuaikan hanya adapter yang berubah. Perbarui fingerprint setelah titik integrasi diperiksa, build terhadap instalasi baru dan periksa bahwa log BepInEx mengaktifkan menu. Uji fail-closed secara terpisah dengan memberikan fingerprint yang sengaja tidak cocok tanpa mengubah file Steam.
4. Ulangi probe dua pemain, controller dan menu fisik, pelepasan/penggantian controller, pickup serempak dan drop, revive, akhir run, join checkpoint dan layout 1080p. Bandingkan performa dengan map dan pengaturan yang sama bila renderer berubah.

Luas perubahan yang diperlukan mengikuti perubahan kualitas, aset, dan kode PEAK pada versi tersebut.


## Ikon ekspedisi

`assets/wooden-trail-sign.png` adalah papan kayu transparan asli untuk tombol menu, juga dari ImageGen bawaan. Prompt yang dipakai:

> Use case: stylized-concept. A single ORIGINAL empty wooden trail wayfinding sign for a playful camping-game menu button. Extremely wide horizontal board, about 5.4:1 width-to-height, with a short blunt angled point at the right end and gently imperfect rounded corners. Straight level orientation, perfectly front-facing flat orthographic view. Golden honey-tan warm wood face, loose low-contrast hand-painted grain and broad irregular strokes, slightly darker reddish-brown shallow bottom bevel. Flat stylized low-poly game art, rustic and cheerful, high contrast with very dark brown lettering that will be added later. Entire face is empty. Center the board horizontally and vertically, span 90% of a wide landscape canvas with generous transparent margins, no pole, no nails, no rope, no external shadow, no text, no symbols, no watermark. Genuinely transparent alpha outside the board.

Ikon perangkat versi 0.1.2 dibuat dengan ImageGen. `assets/keyboard-mouse.png` menggambarkan satu keyboard beserta mouse kabel. `assets/gamepad.png` menggambarkan controller dengan D-pad dan empat tombol muka. Keduanya memakai kontur hijau tua, warna krem dan aksen jingga. Ikon dipaketkan pada lebar 192 piksel dan ditampilkan sesuai rasio aslinya di kartu kursi.

`assets/trail-roster.png` berasal dari mockup lobi yang dipilih pengguna. ImageGen memisahkan papan kayu, kertas, paku dan bendera menjadi latar transparan. `TrailLobby` menempatkan kartu kursi, label, ikon perangkat, tanda centang dan tombol uGUI di atas latar. Teks perangkat bebas hanya tampil jika ada controller yang belum mengisi kursi. Warna tombol menu utama memakai state normal, hover dan fokus bawaan dari Unity Button.

Pada 0.1.4, tombol Local Co-op memakai instance tombol Play Offline dari scene Title sebagai templat. Referensi klik diganti dengan aksi lobi. Ikon `assets/helping-hand.png` merupakan piktogram transparan dua pendaki yang disetujui pengguna dan dibuat dengan ImageGen.

`GuestReticle` memasang varian bidikan PEAK pada setiap canvas tamu. `GUIManager.UpdateReticle` menjalankan aturan yang sama untuk warna bidikan, panjat, tali, lempar dan bidik. Referensi karakter, varian, dan jeda animasi dipasang selama pemanggilan lalu dikembalikan di blok `finally`. Efek dari `CharacterMovement.Land`, `CharacterClimbing.RPCA_ClimbJump`, `CharacterGrabbing.Reach`, dan `EnviroGrab` diarahkan ke bidikan kursi pemilik aksi.

Pada 0.1.5, ikon dua pendaki menjadi anak dari tempat ikon asli Play Offline, sehingga offset lokal dan rotasinya mengikuti papan ketika animasi native berjalan. Bidikan tamu mengambil grup `Reticles` utuh dan mencari referensi Image yang sesuai dengan jalur objek pada grup asli. `GUIManager.UpdateThrow` juga berjalan dalam konteks tamu untuk menampilkan indikator isi dan warna saat pemain menahan lempar. Referensi bidikan dan meter P1 dipulihkan pada akhir setiap pemanggilan.

## Performa Local Co-op 0.1.6

`SeatActions` menyimpan akses field Input System setelah perangkat dipasangkan. Jalur `CameraLook` hanya meneruskan skema perangkat karena kode asli membaca look input yang sudah disampel. `LocalCoopUi.Draw` hanya menggambar pemberitahuan pada repaint, dan UI memakai hasil ketersediaan checkpoint yang dihitung sesi. Perekaman pada recorder prefab tamu dinonaktifkan di room lokal agar input mikrofon tidak diproses per kursi.

`Play Local Co-op DX11.cmd` memakai parameter Steam per peluncuran. Deteksi flag `-peak-coop-dx11` mengaktifkan `DirectCoopLaunch` dan lobi awal khusus. Titik masuk solo dan online diblokir hanya dalam proses tersebut. `MainMenu.StartSoloSession` diizinkan ketika `PartyRuntime` menandai permintaan untuk membuat room Local Co-op offline. Keluar dari sesi menutup proses khusus sebelum pemain bisa memasuki mode lain. Jalur peluncuran normal tidak memasang hook ini. Periksa ulang simbol menu jika PEAK diperbarui.


## Arah item dan pilihan target

`ItemSeat` mengalihkan referensi kamera dan karakter global ke pemegang item tamu untuk fungsi yang diaudit. `RopeShooter.WillAttach` memakai posisi, arah, dan status berpijak P2. `OnPrimaryFinishedCast` memakai kamera yang sama untuk titik tembak dan arah terbang. `RopeTier.Update` memakai input dan kamera pemegang tali. Adapter yang sama menangani `Action_RaycastDart`, `Action_LaunchPlayer`, `Action_RaycastSpawnSomething`, `Action_CloneSelectedItem`, serta penentuan target teman pada jalur secondary item. RPC tetap memakai data target yang dihitung oleh jalur asli.
