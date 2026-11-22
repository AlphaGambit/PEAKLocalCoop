using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Zorro.ControllerSupport;
using Object = UnityEngine.Object;

namespace PeakLocalCoop;

internal sealed class TrailLobby : IDisposable, INavigationContainer
{
    private readonly RectTransform overlay;
    private readonly GameObject previousSelection;
    private readonly Button backButton;
    private readonly Button startButton;
    private readonly TextMeshProUGUI[] devices = new TextMeshProUGUI[4];
    private readonly RawImage[] deviceIcons = new RawImage[4];
    private readonly Image[] seatCards = new Image[4];
    private readonly RectTransform roster;
    private readonly TextMeshProUGUI freeDevices;
    private readonly GameObject[] checks = new GameObject[4];
    private readonly GameObject[] emptyBorders = new GameObject[4];

    public static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    public static Image Panel(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        var image = Rect(parent, name, size, position).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI Text(Transform parent, TMP_FontAsset font, string value,
        Vector2 size, Vector2 position, float typeSize, Color color, TextAlignmentOptions alignment)
    {
        var label = Rect(parent, "Label", size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = value;
        label.fontSize = typeSize;
        label.alignment = alignment;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    public static Button Sign(Transform parent, string name, Vector2 size, Vector2 position, Color color,
        Texture2D texture = null, UnityEngine.Rect uv = default)
    {
        var rect = Rect(parent, name, size, position);
        Graphic image;
        if (texture)
        {
            var wood = rect.gameObject.AddComponent<RawImage>();
            wood.texture = texture;
            wood.uvRect = uv;
            image = wood;
        }
        else image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        var shadow = image.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color32(47, 32, 16, 155);
        shadow.effectDistance = new Vector2(0, -5);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1.16f, 1.12f, 1.04f),
            pressedColor = new Color(0.86f, 0.81f, 0.73f),
            selectedColor = new Color(1.16f, 1.12f, 1.04f),
            disabledColor = new Color(0.7f, 0.7f, 0.7f),
            colorMultiplier = 1,
            fadeDuration = 0.08f
        };
        return button;
    }

    public static void Icon(Transform parent, Texture2D texture, float size, Vector2 position)
    {
        if (!texture) return;
        var image = Rect(parent, "Helping hand icon", Vector2.one * size, position).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
    }

    private static void Artwork(Transform parent, string name, Texture2D texture, Vector2 size, Vector2 position)
    {
        var image = Rect(parent, name, size, position).gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.raycastTarget = false;
    }

    public TrailLobby(Canvas canvas, TMP_FontAsset font, Texture2D frame, Texture2D board,
        Texture2D keyboard, Texture2D gamepad, Action back, Action start, string backLabel)
    {
        previousSelection = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        overlay = Rect(canvas.transform, "Local Co-op Lobby", Vector2.zero, Vector2.zero);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.SetAsLastSibling();
        overlay.gameObject.AddComponent<Image>().color = new Color32(42, 38, 26, 142);
        roster = Rect(overlay, "Expedition board", new Vector2(940, 800), Vector2.zero);
        if (frame)
            Artwork(roster, "Wood, paper and flag", frame, roster.sizeDelta, Vector2.zero);
        else
        {
            Panel(roster, "Parchment", new Vector2(840, 710), Vector2.zero, TrailPalette.Paper);
            Panel(roster, "Header", new Vector2(740, 110), new Vector2(0, 240), TrailPalette.Pine);
        }
        Text(roster, font, "LOCAL CO-OP", new Vector2(610, 106), new Vector2(-49, 240),
            59, TrailPalette.Cream, TextAlignmentOptions.Center).overflowMode = TextOverflowModes.Overflow;
        Text(roster, font, "TEKAN START / MENU UNTUK BERGABUNG", new Vector2(740, 52),
            new Vector2(0, 151), 28, TrailPalette.Ink, TextAlignmentOptions.Center);

        for (var i = 0; i < seatCards.Length; i++)
        {
            var position = new Vector2(i % 2 == 0 ? -196 : 196, i < 2 ? 65 : -87);
            var card = Panel(roster, $"Scout P{i + 1}", new Vector2(368, 126), position, TrailPalette.Canvas);
            seatCards[i] = card;
            var border = Rect(card.transform, "Available outline", new Vector2(368, 126), Vector2.zero);
            emptyBorders[i] = border.gameObject;
            var ink = new Color32(126, 88, 50, 215);
            for (var dash = -165; dash <= 165; dash += 22)
            {
                Panel(border, "Stitch", new Vector2(13, 3), new Vector2(dash, 61), ink);
                Panel(border, "Stitch", new Vector2(13, 3), new Vector2(dash, -61), ink);
            }
            for (var dash = -44; dash <= 44; dash += 22)
            {
                Panel(border, "Stitch", new Vector2(3, 13), new Vector2(-182, dash), ink);
                Panel(border, "Stitch", new Vector2(3, 13), new Vector2(182, dash), ink);
            }
            var number = Panel(card.transform, "Seat marker", new Vector2(68, 60), new Vector2(-129, 15), TrailPalette.Pine);
            Text(number.transform, font, $"P{i + 1}", new Vector2(68, 60), Vector2.zero,
                35, TrailPalette.Cream, TextAlignmentOptions.Center).overflowMode = TextOverflowModes.Overflow;

            var device = i == 0 ? keyboard : gamepad;
            var aspect = device ? (float)device.height / device.width : 0.6f;
            var glyph = Rect(card.transform, "Device glyph", new Vector2(120, 120 * aspect),
                new Vector2(8, 17)).gameObject.AddComponent<RawImage>();
            glyph.texture = device;
            glyph.raycastTarget = false;
            deviceIcons[i] = glyph;
            var check = Panel(card.transform, "Connected", new Vector2(38, 38), new Vector2(147, 16), TrailPalette.Pine);
            var checkMark = Rect(check.transform, "Tick", new Vector2(24, 24), new Vector2(0, 2));
            checkMark.localRotation = Quaternion.Euler(0, 0, 45);
            Panel(checkMark, "Short stroke", new Vector2(5, 11), new Vector2(-5, -4), TrailPalette.Cream);
            Panel(checkMark, "Long stroke", new Vector2(20, 5), new Vector2(3, -8), TrailPalette.Cream);
            checks[i] = check.gameObject;
            devices[i] = Text(card.transform, font, "", new Vector2(328, 54), new Vector2(0, -37),
                23, TrailPalette.Ink, TextAlignmentOptions.Center);
            devices[i].enableAutoSizing = true;
            devices[i].fontSizeMin = 16;
            devices[i].fontSizeMax = 23;
        }

        freeDevices = Text(roster, font, "", new Vector2(733, 42), new Vector2(0, -176),
            16, TrailPalette.Ink, TextAlignmentOptions.Center);
        backButton = Sign(roster, "Back", new Vector2(262, 74), new Vector2(-224, -243),
            new Color(0.85f, 0.81f, 0.69f), board, TitleCoopEntry.BoardUv);
        backButton.transform.localScale = new Vector3(-1, 1, 1);
        startButton = Sign(roster, "Enter Airport", new Vector2(420, 79), new Vector2(145, -243),
            Color.white, board, TitleCoopEntry.BoardUv);
        var backText = Text(backButton.transform, font, backLabel, new Vector2(240, 65), Vector2.zero,
            32, TrailPalette.Ink, TextAlignmentOptions.Center);
        backText.rectTransform.localScale = new Vector3(-1, 1, 1);
        Text(startButton.transform, font, "MASUK AIRPORT", new Vector2(375, 70), new Vector2(-3, 0),
            37, TrailPalette.Ink, TextAlignmentOptions.Center);
        backButton.onClick.AddListener(() => back());
        startButton.onClick.AddListener(() => start());
        backButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = startButton };
        startButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = backButton };
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(startButton.gameObject);
    }

    public bool Visible => overlay && overlay.gameObject.activeInHierarchy;
    public int GetContainerPriority() => 50;
    public GameObject GetDefaultSelection() => Visible ? startButton.gameObject : null;
    public bool IsValidSelection(GameObject selection) => Visible &&
        (selection == backButton.gameObject || selection == startButton.gameObject);

    public void Refresh(LocalParty party)
    {
        INavigationContainer.PushActive(this);
        var bounds = ((RectTransform)overlay.parent).rect;
        var fit = Mathf.Min(bounds.width / 1030f, bounds.height / 1000f);
        roster.localScale = Vector3.one * fit;
        roster.anchoredPosition = new Vector2(Mathf.Clamp((bounds.width - roster.sizeDelta.x * fit) * 0.16f, 0, 175), 0);
        for (var i = 0; i < devices.Length; i++)
        {
            var seat = i < party.Seats.Count ? party.Seats[i] : null;
            var connected = i == 0 || (seat?.Pad != null && seat.Pad.added);
            emptyBorders[i].SetActive(!connected);
            checks[i].SetActive(connected);
            deviceIcons[i].color = connected ? Color.white : new Color(0.80f, 0.75f, 0.62f, 0.90f);
            seatCards[i].color = connected ? new Color32(198, 206, 138, 255) : new Color32(239, 204, 145, 255);
            devices[i].text = i == 0 ? "KEYBOARD + MOUSE" : seat == null ? "TEKAN START" :
                connected ? NameOf(seat.Pad) : "CONTROLLER TERPUTUS";
        }
        var available = Gamepad.all.Where(pad => !party.Seats.Any(seat => seat.Matches(pad))).Select(NameOf).ToArray();
        freeDevices.text = available.Length > 0
            ? $"Tersedia  ·  {string.Join("  /  ", available)}" : "";
    }

    private static string NameOf(Gamepad pad) => $"{pad.displayName}  #{pad.deviceId}";

    public void Dispose()
    {
        if (!overlay) return;
        overlay.gameObject.SetActive(false);
        Object.Destroy(overlay.gameObject);
        if (EventSystem.current)
            EventSystem.current.SetSelectedGameObject(previousSelection && previousSelection.activeInHierarchy
                ? previousSelection : null);
    }
}
