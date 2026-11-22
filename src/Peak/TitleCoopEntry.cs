using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class TitleCoopEntry : IDisposable
{
    private readonly Action open;
    private readonly bool directLaunch;
    private GameObject hiddenRows;
    private readonly ManualLogSource log;
    private readonly Texture2D climbingIcon;
    private readonly Texture2D wood;
    private MainMenuMainPage page;
    private Button button;
    private Button solo;
    private Button settings;
    private Navigation soloNav;
    private Navigation settingsNav;
    private readonly List<(RectTransform Row, Vector2 Position)> originalRows = new();
    private bool expanded;
    private bool failed;
    public Canvas Canvas { get; private set; }
    public TMP_FontAsset Font { get; private set; }
    public Texture2D Board => wood;
    public Texture2D Roster { get; }
    // Exclude the transparent margins baked into the sign artwork.
    public static readonly Rect BoardUv = new Rect(31f / 2172f, 164f / 724f, 2111f / 2172f, 400f / 724f);
    public Texture2D KeyboardIcon { get; }
    public Texture2D GamepadIcon { get; }
    public Button Button => button;

    public TitleCoopEntry(Action open, ManualLogSource log, bool directLaunch)
    {
        this.open = open;
        this.directLaunch = directLaunch;
        this.log = log;
        climbingIcon = LoadImage("HelpingHand.png");
        KeyboardIcon = LoadImage("KeyboardMouse.png");
        GamepadIcon = LoadImage("Gamepad.png");
        wood = LoadImage("WoodenTrailSign.png");
        Roster = LoadImage("TrailRoster.png");
    }

    private Texture2D LoadImage(string filename)
    {
        using var stream = typeof(TitleCoopEntry).Assembly.GetManifestResourceStream("PeakLocalCoop." + filename);
        if (stream != null)
        {
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = filename,
                wrapMode = TextureWrapMode.Clamp
            };
            if (texture.LoadImage(bytes.ToArray(), markNonReadable: true)) return texture;
            Object.Destroy(texture);
        }
        log.LogWarning($"Aset UI Local Co-op tidak dapat dibaca. {filename}");
        return null;
    }

    public void Update()
    {
        if (SceneManager.GetActiveScene().name != MainMenu.SceneName)
        {
            if (!ReferenceEquals(page, null)) Detach();
            failed = false;
            return;
        }
        if (!page && !failed)
        {
            var target = Object.FindFirstObjectByType<MainMenuMainPage>(FindObjectsInactive.Include);
            if (!target) return;
            try { Attach(target); }
            catch (Exception error)
            {
                Detach();
                failed = true;
                log.LogError($"Entri Local Co-op gagal dipasang pada menu Title. {error}");
                if (directLaunch) Application.Quit();
            }
        }
        if (!button || !solo) return;
        var visible = solo.gameObject.activeSelf && solo.interactable;
        if (visible != expanded) SetExpanded(visible);
    }

    private void Attach(MainMenuMainPage target)
    {
        page = target;
        solo = FindButton("m_soloButton");
        settings = FindButton("m_settingsButton");
        soloNav = solo.navigation;
        settingsNav = settings.navigation;
        Canvas = solo.GetComponentInParent<Canvas>(includeInactive: true);
        var nativeLabel = solo.GetComponentInChildren<TMP_Text>(includeInactive: true);
        Font = nativeLabel ? nativeLabel.font : null;
        if (!Canvas || !Font) throw new InvalidOperationException("Canvas atau font Play Solo tidak ditemukan.");

        var soloRect = (RectTransform)solo.transform;
        var settingsRect = (RectTransform)settings.transform;
        var parent = soloRect.parent;
        if (directLaunch) return;
        if (settingsRect.parent != parent || !(soloRect.anchoredPosition.y > settingsRect.anchoredPosition.y))
            throw new InvalidOperationException("Urutan Play Solo dan Settings berubah.");
        foreach (Transform child in parent)
            if (child is RectTransform row && child.GetComponent<Button>())
                originalRows.Add((row, row.anchoredPosition));

        button = Object.Instantiate(solo, parent, false);
        button.name = "Button_LocalCoop";
        button.onClick = new Button.ButtonClickedEvent();
        var rect = (RectTransform)button.transform;
        var clonedLabel = button.GetComponentInChildren<TMP_Text>(includeInactive: true);
        foreach (var text in clonedLabel.GetComponents<LocalizedText>()) text.enabled = false;
        clonedLabel.text = "LOCAL CO-OP";
        var nativeIcons = Array.FindAll(button.GetComponentsInChildren<Image>(true), image =>
            image != button.targetGraphic && (image.name == "Image" || image.name.StartsWith("Image (", StringComparison.Ordinal)));
        var iconSocket = Array.Find(nativeIcons, image => image.enabled && image.gameObject.activeSelf);
        if (!iconSocket) throw new InvalidOperationException("Tempat ikon Play Offline tidak ditemukan.");
        foreach (var image in nativeIcons) image.enabled = false;
        TrailLobby.Icon(iconSocket.rectTransform, climbingIcon, 42, Vector2.zero);
        rect.anchoredPosition = new Vector2(soloRect.anchoredPosition.x - 8,
            (soloRect.anchoredPosition.y + settingsRect.anchoredPosition.y) / 2f);
        rect.SetSiblingIndex(soloRect.GetSiblingIndex() + 1);

        button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = solo, selectOnDown = settings };
        button.onClick.AddListener(() => open());
        button.gameObject.SetActive(false);
    }

    public bool PrepareDirectLobby()
    {
        if (hiddenRows) return true;
        if (!solo || !solo.isActiveAndEnabled || !solo.IsInteractable()) return false;
        hiddenRows = solo.transform.parent.gameObject;
        hiddenRows.SetActive(false);
        return true;
    }

    private Button FindButton(string name) =>
        BuildGuard.Field(typeof(MainMenuMainPage), name).GetValue(page) as Button
        ?? throw new InvalidOperationException($"{name} tidak ditemukan di menu Title.");

    private void SetExpanded(bool visible)
    {
        var soloY = originalRows.Find(entry => entry.Row == solo.transform).Position.y;
        foreach (var (row, position) in originalRows)
            if (row)
                row.anchoredPosition = visible
                    ? position + Vector2.up * (position.y >= soloY ? 26f : -24f)
                    : position;
        var nextSolo = soloNav;
        var nextSettings = settingsNav;
        if (visible)
        {
            nextSolo.selectOnDown = button;
            nextSettings.selectOnUp = button;
        }
        solo.navigation = nextSolo;
        settings.navigation = nextSettings;
        button.gameObject.SetActive(visible);
        expanded = visible;
    }

    private void Detach()
    {
        if (hiddenRows) hiddenRows.SetActive(true);
        hiddenRows = null;
        if (expanded && solo && settings && button) SetExpanded(false);
        if (button) Object.Destroy(button.gameObject);
        originalRows.Clear();
        expanded = false;
        page = null;
        button = null;
        solo = null;
        settings = null;
        Canvas = null;
        Font = null;
    }

    public void Dispose()
    {
        Detach();
        if (climbingIcon) Object.Destroy(climbingIcon);
        if (wood) Object.Destroy(wood);
        if (Roster) Object.Destroy(Roster);
        if (KeyboardIcon) Object.Destroy(KeyboardIcon);
        if (GamepadIcon) Object.Destroy(GamepadIcon);
    }
}
