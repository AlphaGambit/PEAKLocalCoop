# Audit PEAK untuk local co-op

## Identitas instalasi

Audit read-only dimulai 21 November 2026, waktu Bangkok. Bukti berasal dari instalasi Steam Windows x64. ILSpy 9.1.0.7988 digunakan untuk membaca IL di luar direktori game. Pembacaan aset ter-serialisasi dilakukan dengan UnityPy. Paket mod tidak menyertakan dekompilasi, aset atau assembly game.

| Parameter | Nilai yang dibaca |
| --- | --- |
| Steam App ID dan Build ID | 3527290 dan 26542162 |
| Depot Windows dan manifest | 3527291 dan 719488143636267246 |
| `version.txt` | `3.1.0--logFile`, `55f91cd1d` |
| Unity | 6000.3.15f1, Windows x64, Mono |
| Build GUID Unity | `fb9ca7d1a6204a22afc4b5f64560fa83` |
| `Assembly-CSharp.dll` SHA-256 | `a3643f11dc444d86491270b224c0a0caf9126a10b26731feb97fc85ad1d82038` |
| `PhotonUnityNetworking.dll` SHA-256 | `d0aa090013a5327d35e642dcdcc4797857b81a994d50eba3394aeb24fa5cd11f` |
| `Unity.InputSystem.dll` SHA-256 | `acfe2788d3f891022178c0ee1bf4d307473a99079925b3cbb2418dde8e615850` |
| `globalgamemanagers` SHA-256 | `81806cade6c480a9ba4beb0b13c686d3785fe3e6a5d422d4f7da0e472455a5e1` |

## Peta integrasi

Kolom bukti menjelaskan apa yang dibaca dari build. Perilaku yang membutuhkan eksekusi dicatat terpisah.

| Sistem | Bukti dari build |
| --- | --- |
| Menu dan solo | `Pretitle.PreloadScene` beralih ke `Title`. `MainMenu.PlaySoloClicked` → `StartSoloSession` → `StartOfflineModeRoutine` → `DisconnectForOfflineMode` dan `LoadingScreenHandler.LoadSceneProcess("Airport", …)`. `NetworkConnector.Start` memanggil `NetworkingUtilities.JoinDummyRoom` yang membuat room lewat PUN. |
| Karakter asli | `CharacterSpawner.SpawnHostCharacter` → `SpawnMyPlayerCharacter`. Prefab Resource `Character` dan `Player` dibuat melalui `PhotonNetwork.Instantiate`. Untuk run baru, spawner memakai SpawnPoint, memulai pose pingsan di pantai jika dikonfigurasi, dan menginisialisasi scout report. `RunStarter.Start` menunggu room, karakter lokal, dan loading selesai sebelum memanggil `RunManager.StartRun`. |
| Referensi karakter dan state | `Character.Awake` / `Player.Awake` mengisi singleton host ketika `PhotonView.IsMine` benar. `PlayerHandler.RegisterCharacter` dan `RegisterPlayerImpl` menggunakan ActorNumber, dan mengganti objek yang sudah terdaftar pada nomor itu. `Character.player` mengambil inventory dari owner view. `Character.data` menyimpan look dan stamina per objek, `CharacterAfflictions` menyimpan status per karakter. |
| Simulasi dan otoritas | `CharacterMovement.Update` mengambil sampel input bila `Character.IsLocal`. `CharacterMovement.FixedUpdate` menghitung gaya ragdoll. `Character.Update` dan beberapa jalur lain memakai `PhotonView.IsMine` untuk simulasi pemilik/controller. `CharacterSyncer` memakai remote snapshot untuk view nonlokal. Keputusan seperti pickup dan drop memeriksa `PhotonNetwork.IsMasterClient`. |
| Photon offline | `PhotonNetwork.EnterOfflineRoom` menetapkan host sebagai actor 1. `PhotonView.ViewID` membagi ID dengan `MAX_VIEW_IDS` untuk creator dan menurunkan owner. Controller menentukan `IsMine`. `PhotonNetwork.RPC` mengeksekusi `All` di proses yang sama dan `MasterClient` secara langsung bila host lokal. RPC ke actor tertentu hanya mengeksekusi langsung bila nomor target adalah actor lokal Photon. `CachedPlayerList` mengambil `PhotonNetwork.PlayerList` dan menyegarkan berdasarkan jumlah actor. |
| Input gameplay | `CharacterInput.Init` memakai satu `InputSystem.actions` dan menyimpan action, termasuk hotbar, dalam field statis. `Sample` mengisi hasil per objek. `CharacterMovement.CameraLook` memilih sensitivitas dari `InputHandler.GetCurrentUsedInputScheme`. `Character.CanDoInput` membaca jendela dan wheel dari GUI tunggal. |
| Input menu dan HUD | `UIInputHandler.Sample` membaca `UIConfirm`, `UICancel`, tab dan wheel dari asset global. `GUIManager.LateUpdate` memakai `Character.localCharacter` untuk reticle, status dan item. `GUIManager.OpenBackpackWheel` dan `CloseBackpackWheel` menulis status karakter lokal. Canvas `Canvas_HUD` Airport (path ID 14205) dan WilIsland (572524) berjenis Screen Space Overlay dalam aset. |
| Kamera dan listener | `MainCamera.Awake` menetapkan singleton dan memasang `AudioMixerUnifier`. `MainCameraMovement.LateUpdate` memilih sudut pandang berdasarkan host, dan `Character.observedCharacter` memakai `specCharacter` global. Airport memiliki `MainCamera` path ID 8746 dengan viewport penuh dan tanpa target texture, serta `AudioListener` ID 11513. Kamera cermin, paspor dan display punya target texture tersendiri. |
| Pipeline dan efek | `GraphicsSettings.m_CustomRenderPipeline` dan kualitas `High Fidelity` sama-sama menunjuk ke `globalgamemanagers.assets` object 6170, bernama `URPAsset`; MonoScript 4627 adalah `UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset`. `SingleBufferFeature` mengambil target color/depth per kamera. `EyeBlinkController` dan `ItemRenderFeatureManager` mengubah renderer feature bersama. `MirageManager` memakai `Screen.width/height` untuk RT, `RescueHook` memakai pusat layar global. `GraphicsSettings.currentRenderPipeline` memberi konfirmasi runtime pada probe. |
| Interaksi dan inventory | `Interaction.LateUpdate` mengelola target, hold dan cast dari `Character.localCharacter` dan `MainCamera.instance`. `Item.Interact` menonaktifkan objek dan mengirim `RequestPickup` ke master; master memakai `character.player.AddItem`, mengirim `OnPickupAccepted` ke owner dan menghancurkan objek world. `CharacterItems.DropItemRpc` melepas item held, membuat item world lewat `InstantiateItemRoom` di master dan mengosongkan slot yang sesuai. |
| Bantu dan revive | `CharacterGrabbing`, `CharacterClimbing` dan `CharacterCarrying` memakai view/simulasinya sendiri. `CharacterInteractible` memakai `Character.localCharacter` di beberapa pemeriksaan. `Character.ReviveCharacter` mengosongkan status pass-out/death timer. `RPCA_ReviveAtPosition` mewarp scout dan memperbarui `ReconnectHandler`; `MapHandler` memilih revive camp dengan `BaseCampScoutStatue`. |
| Checkpoint | `Campfire.Light_Rpc` mengubah state, memanggil `MapHandler.GoToSegment` dan menulis quicksave. `Campfire.Lit` dan `EveryoneInRange()` membedakan api aktif dan party yang masih di sekitar. `MapHandler.LastSeenCampfireIsSafe` sendiri dapat bernilai true jauh dari camp. `CharacterSpawner` memakai kombinasi `BaseCampHasRevived` dan `LastSeenCampfireIsSafe` untuk keputusan spawn pemain jaringan. `CurrentBaseCampSpawnPoint` dapat menunjuk ke camp sebelumnya; spawn perlu memastikan anchor checkpoint yang dipakai saat itu. |
| Perpindahan dan akhir run | `AirportCheckInKiosk.StartGame` → `LoadIslandMaster` → `BeginIslandLoadRPC` → loader networked (masih di room offline untuk solo). Satu level dipilih lewat `NextLevelService` dan `MapBaker`; contoh dari log PEAK sebelumnya adalah `Level_95`. `PlayerHandler.ClearSceneData` membersihkan registrasi scene. `Character.CheckEndGame` mengecek semua `AllCharacters` mati di master. `RPCEndGame` menghitung kemenangan per karakter dengan `CheckWinCondition` dan mengakhiri run global. |
| Penyimpanan | `Peak.Quicksave.SaveNow` menulis `quicksave.peak` di `Application.persistentDataPath`. `DestroySaveData` menghapus path tersebut. Jalur vanilla Resume perlu diaudit sebelum mendukung simpan/muat kursi lokal. |

## Integrasi yang dipilih

Pakai `BepInExPack_PEAK` 5.4.75301 (core BepInEx 5.4.23.3) dan Harmony yang dibundel. Mod mengaitkan prefab PEAK ke actor lokal tambahan dalam room PUN offline milik host. Nomor view dan owner terpisah menjaga registrasi dan inventory. Controller view dipindah ke host setelah Awake agar simulasi asli berjalan tanpa mengganti singleton host. ID kursi internal adalah GUID dengan umur sesi; nama tampilan, ID perangkat dan akun Steam terpisah. Adapter membatasi routing ke room offline yang dibuat oleh sesi mod dan memverifikasi fingerprint sebelum memasang hook. Tidak ada transport tambahan atau simulasi dunia kedua.

## Audit referensi P1 pada 0.1.8

Pemindaian IL `Assembly-CSharp.dll` menemukan 327 metode yang membaca singleton karakter, player, interaksi atau kamera. Daftar itu mencakup menu, akun, diagnostik, rendering, serta logika gameplay. Adapter yang diubah dan simbolnya tercatat di [catatan integrasi](INTEGRATION.md).

Area yang masih membutuhkan penanganan atau skenario pengujian terpisah mencakup overlay binokular dan override kamera, spektator dan sinematik summit, hazard khusus yang mengenali satu karakter lokal seperti `BananaPeel.Update`, `MovingSawBlade.OnTriggerStay` dan `SpikeRoller.OnCollisionEnter`, serta efek khusus item seperti `BingBongShieldWhileHolding` dan `Affliction_MassSuperJump.OnApplied`. Inspeksi menunjukkan ketergantungan pada singleton. Dampak runtime dan perilaku yang tepat untuk tiap kasus ini perlu diuji dengan prefab dan biome terkait.

## Audit lanjutan 0.1.9

| Jalur | Temuan dari IL | Status |
| --- | --- | --- |
| Penanda lokasi dan lintasan kamera | `PointPinger.TryGetPingHit` memakai `Camera.main`; `PointPing.Go` juga memakai kamera dan jarak P1 untuk skala serta orientasi visual marker dunia. | Target raycast dan kelayakan pembuatan marker sudah diarahkan per kursi. Skala dan orientasi visual marker dunia masih memakai jalur native P1. |
| D-pad dan tombol bahu | `CharacterItems.DoSwitching` menangani kiri, kanan dan bawah. `GUIManager.UpdateEmoteWheel` hanya membuka roda karakter lokal. `RopeSpool.Update` membaca input pemegang dan `CharacterInput.Sample` menyisihkan aksi bumper untuk kombo voice. | Roda emote, scroll, toggle ransel dan navigasi penonton diadaptasi. Lihat [hasil uji](VERIFICATION.md). |
| Binokular dan override kamera | `Action_ShowBinocularOverlay.RunAction` serta `Action_OverrideCamera.RunAction` memanggil `MainCamera.SetCameraOverride`. `CameraOverride_Binoculars.Update` masih membaca karakter P1. | Overlay, zoom dan efek sun glare belum terpisah per panel. |
| Kulit pisang dan perangkap | `BananaPeel.Update` membaca jarak, grounded, kecepatan serta view yang akan terpeleset dari P1. `MovingSawBlade.OnTriggerStay` dan `SpikeRoller.OnCollisionEnter` mengenali collider karakter yang benar, lalu membatasi cedera ke P1. Keduanya memiliki cooldown pada instance perangkap. | Memerlukan pengujian benturan masing-masing scout dan keputusan cooldown per pemain. |
| Tomb | `TombTrigger.OnTriggerEnter` menemukan karakter dari collider tetapi pemanggilan `TriggerTomb` dan perubahan audio dibatasi ke P1. | Pemicu oleh pemain tamu dan audio area belum terverifikasi. |
| Item khusus | `BingBongShieldWhileHolding` mencari item di tangan P1 saat memasang atau menghapus shield. `Affliction_MassSuperJump.OnApplied` langsung keluar setelah menaikkan karakter jika `IsLocal` benar, sehingga cabang radius remote tidak dipakai untuk karakter yang juga lokal. | Risiko penerima efek dan penghapusan status masih terbuka. |
| Kemenangan summit | `PeakSequence.CheckGameComplete` mengumpulkan karakter terdaftar dan menguji `Character.CheckWinCondition` untuk setiap peserta. Setelah timer habis, P1 memanggil `EndGame` untuk sesi. | Hitung peserta telah ditelusuri. Kondisi akhir, sinematik dan report saat sebagian pemain mencapai puncak belum diuji. |


## Audit kematian dan cedera pada 0.1.10

`CharacterAfflictions.shouldPassOut` memakai jumlah status di atas 0,99. `Character.UpdateVariablesFixed` memajukan timer selama pingsan. Jalur default sekitar 60 detik simulasi saat ada scout lain yang mampu menolong. Menggendong, menahan tombol interaksi, kondisi seluruh party, status sementara, atau flag personal dapat mengubah hasil atau kecepatannya menurut mekanik PEAK. `RecalculateInvincibility` membaca status invincibility atau shield per karakter. Patch co-op mempertahankan perlindungan native tersebut.

Hantu dibuat oleh `MainCameraMovement.Spectate` hanya setelah karakter mati. Implementasi sebelumnya memakai kamera split-screen independen dan tidak memanggil jalur pembuatan hantu untuk tamu. Renderers hantu juga awalnya dimatikan untuk semua kamera ketika PhotonView pemilik dianggap lokal. Kedua asumsi itu ditangani oleh [adapter hantu](INTEGRATION.md).

Filter P1 pada gergaji bergerak dan spike roller diperbaiki. Kulit pisang, kapak ayun, dan efek pelindung item khusus masih memakai jalur tersendiri. Kondisi invincibility yang dialami pengguna belum bisa dihubungkan dengan satu efek spesifik tanpa sumber benturan dan status karakter saat kejadian.

## Audit lanjutan 0.1.11

`SwingingAxe.OnCollisionEnter` memasukkan karakter korban ke field closure `hitCharacter`. Helper `BonkScout` kembali membandingkannya dengan P1 sebelum menjalankan gaya, efek jatuh dan cedera. `BananaPeel.Update` memakai P1 untuk memilih korban dan mengirim view penerima. `Constructable` memakai `MainCamera.instance` untuk preview serta singleton P1 untuk mengurangi inventory. `PointPing.Go` memakai kamera utama dan jarak P1 ketika menentukan skala atau orientasi. Integrasi terbaru mengarahkan empat jalur ini ke karakter atau panel terkait.

Binokular masih membutuhkan isolasi `ItemRenderFeatureManager.setFeatureActive` dan `MainCamera.SetCameraOverride`. Kamera tamu memakai renderer URP yang sama. Mengalihkan target kamera saja masih berisiko mengaktifkan overlay untuk seluruh panel. Simpan/muat party, semua biome, controller fisik dan kemenangan di summit tetap memiliki skenario uji tersendiri.

### Risiko yang masih perlu diuji

- Perlu memeriksa asumsi singleton di atas dalam jalur item khusus, wheel, aksi NPC atau hazard, revive, spektator, cutscene, dan sinkronisasi report kemenangan. Bukti bahwa satu item bekerja tidak menggeneralisasi ke semua item.
- Join pada camp Beach → Tropics, footprint spawn, dan penutupan gate setelah keluar radius terverifikasi. Campfire lain, status lava dan kombinasi custom run perlu diperiksa.
- Viewport langsung adalah pilihan awal. Efek yang mengambil target global atau skala layar perlu uji visual dan profiling per kamera. Referensi eksternal menjelaskan API; path asset dan simbol di atas berasal dari build lokal.

## Rujukan

- [Co-op Anything · Split-screen](https://github.com/cranebeaker/coop-anything/blob/main/playbook/split-screen.md) dan [Reverse engineering](https://github.com/cranebeaker/coop-anything/blob/main/playbook/reverse-engineering.md)
- [Unity · Camera.rect](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Camera-rect.html) dan [pipeline yang aktif](https://docs.unity.com/en-us/engine/6000.0/manual/render-pipelines/choose-a-render-pipeline/srp-setting-render-pipeline-asset)
- [PEAK FAQ · Landfall](https://landfall.se/peak-faq) dan [PEAK · Steam](https://store.steampowered.com/app/3527290/PEAK/)
- [BepInExPack PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/v/5.4.75301/)
- [Pembanding LocalMultiplayer PEAK](https://thunderstore.io/c/peak/p/quackandcheese/LocalMultiplayer/) serta [Steamworks · ISteamUser](https://partner.steamgames.com/doc/api/ISteamUser#GetSteamID) untuk fase online.
