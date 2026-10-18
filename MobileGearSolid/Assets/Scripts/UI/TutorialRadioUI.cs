using UnityEngine;
using UnityEngine.UI;

namespace MobileGearSolid
{
    /// Presentation only. GameUI owns typing, input, the Dialog phase and its completion callback.
    public sealed class TutorialRadioUI : MonoBehaviour
    {
        static readonly Color Green = Hex(0x8CFF99), Amber = Hex(0xFFC74D);
        static readonly Color Copy = Hex(0xDBE6DB), Muted = Hex(0x8C9E91);
        RectTransform root, backdrop, row, receiver, portraitFrame, speech, window, cursor;
        Image receiverImage, portrait, lamp;
        Image[] portraitBorder, speechBorder;
        Text heading, nameText, connection, direction, receiverDirection;
        Image rule;
        Font font;
        bool boss;
        Vector2 lastSize;
        public Text Replica { get; private set; }
        public Text Hint { get; private set; }
        public bool Visible => gameObject.activeSelf;

        public static TutorialRadioUI Create(RectTransform parent, RectTransform hud)
        {
            var rt = UIKit.Rect(parent, "TutorialRadio").Fill();
            var radio = rt.gameObject.AddComponent<TutorialRadioUI>();
            radio.root = rt;
            radio.font = Resources.Load<Font>("Fonts/PTMono-Regular");
            // The opaque backdrop precedes the existing HUD. Its contents stay live and in place.
            radio.backdrop = UIKit.Img(hud.parent, "TutorialRadioBackdrop", Hex(0x090F0D)).rectTransform.Fill();
            radio.backdrop.GetComponent<Image>().raycastTarget = false;
            radio.backdrop.SetSiblingIndex(hud.GetSiblingIndex());
            radio.Build();
            radio.SetVisible(false);
            return radio;
        }

        static Color Hex(uint rgb) => (Color)new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        static Image Image(Transform parent, string id, Color color)
        {
            var image = UIKit.Img(parent, id, color);
            image.raycastTarget = false;
            return image;
        }
        Text Label(Transform parent, string id, string copy, int size, Color color, TextAnchor align)
        {
            var t = UIKit.Rect(parent, id).gameObject.AddComponent<Text>();
            t.font = font;
            t.fontStyle = FontStyle.Normal;
            t.fontSize = size;
            t.text = copy;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.supportRichText = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        static void Top(RectTransform rt, float x, float y, float w, float h) =>
            rt.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));
        static Image[] Border(RectTransform parent, Color tint, bool inside = false)
        {
            // Four square strips, independent of the general UI styling branch.
            var images = new Image[4];
            for (int i = 0; i < 4; i++) images[i] = Image(parent, "Border" + i, tint);
            images[0].rectTransform.anchorMin = new Vector2(0, 1); images[0].rectTransform.anchorMax = Vector2.one;
            images[1].rectTransform.anchorMin = Vector2.zero; images[1].rectTransform.anchorMax = new Vector2(1, 0);
            images[2].rectTransform.anchorMin = Vector2.zero; images[2].rectTransform.anchorMax = new Vector2(0, 1);
            images[3].rectTransform.anchorMin = new Vector2(1, 0); images[3].rectTransform.anchorMax = Vector2.one;
            images[0].rectTransform.offsetMin = new Vector2(0, 0); images[0].rectTransform.offsetMax = new Vector2(0, 3);
            images[1].rectTransform.offsetMin = new Vector2(0, -3); images[1].rectTransform.offsetMax = Vector2.zero;
            images[2].rectTransform.offsetMin = new Vector2(-3, -3); images[2].rectTransform.offsetMax = new Vector2(0, 3);
            images[3].rectTransform.offsetMin = new Vector2(0, -3); images[3].rectTransform.offsetMax = new Vector2(3, 3);
            if (inside)
            {
                images[0].rectTransform.offsetMin = new Vector2(0, -3); images[0].rectTransform.offsetMax = Vector2.zero;
                images[1].rectTransform.offsetMin = Vector2.zero; images[1].rectTransform.offsetMax = new Vector2(0, 3);
                images[2].rectTransform.offsetMin = Vector2.zero; images[2].rectTransform.offsetMax = new Vector2(3, 0);
                images[3].rectTransform.offsetMin = new Vector2(-3, 0); images[3].rectTransform.offsetMax = Vector2.zero;
            }
            return images;
        }
        void Build()
        {
            for (int side = 0; side < 2; side++)
            {
                var edge = Image(backdrop, "BrickEdge", Hex(0x0A1412)).rectTransform;
                edge.At(new Vector2(side, .5f), new Vector2(side, .5f), Vector2.zero, new Vector2(30, 0));
                edge.anchorMin = new Vector2(side, 0); edge.anchorMax = new Vector2(side, 1);
                for (int y = 0; y < 64; y++)
                    Image(edge, "Mortar", Hex(0x070E0C)).rectTransform.At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y * 36), new Vector2(30, 7.5f));
            }
            heading = Label(root, "Heading", Loc.T("radio.heading"), 41, Green, TextAnchor.MiddleCenter);
            heading.gameObject.AddComponent<RadioTextTracking>().Spacing = 4.5f;
            row = UIKit.Rect(root, "RadioRow");
            receiverImage = Image(row, "Receiver", Color.white); receiver = receiverImage.rectTransform;
            receiverDirection = Label(receiver, "ReceiverDirection", "", 41, Copy, TextAnchor.MiddleCenter);
            receiverDirection.rectTransform.anchorMin = new Vector2(35f / 680, 1 - 158f / 244);
            receiverDirection.rectTransform.anchorMax = new Vector2(186f / 680, 1 - 112f / 244);
            receiverDirection.rectTransform.offsetMin = receiverDirection.rectTransform.offsetMax = Vector2.zero;
            portraitFrame = Image(row, "PortraitFrame", Hex(0x163522)).rectTransform;
            portraitBorder = Border(portraitFrame, Hex(0x597D5D));
            var clip = UIKit.Rect(portraitFrame, "Clip").Fill(); clip.gameObject.AddComponent<RectMask2D>();
            portrait = Image(clip, "Portrait", Color.white);
            portrait.preserveAspect = true;
            for (int i = 0; i < 48; i++)
                Image(clip, "Scan", new Color(Green.r, Green.g, Green.b, 9 / 255f)).rectTransform
                    .At(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 12), new Vector2(288, 3));
            lamp = Image(row, "ConnectionLamp", Green);
            connection = Label(row, "Connection", "", 23, Green, TextAnchor.MiddleLeft);
            speech = UIKit.Rect(root, "Speech");
            nameText = Label(speech, "Speaker", "", 36, Green, TextAnchor.MiddleLeft);
            rule = Image(speech, "SpeakerRule", Hex(0x294C36));
            window = Image(speech, "SpeechWindow", Hex(0x0D1C14)).rectTransform;
            speechBorder = Border(window, Green, true);
            Replica = Label(window, "Replica", "", 36, Copy, TextAnchor.UpperLeft);
            // uGUI includes the font's native leading. Measure it instead of assuming 1 Unity line = 1 em.
            var settings = Replica.GetGenerationSettings(new Vector2(1500, 1000));
            settings.lineSpacing = 1;
            settings.scaleFactor = 1;
            var generator = new TextGenerator();
            float pitch = generator.GetPreferredHeight("А\nА", settings) - generator.GetPreferredHeight("А", settings);
            Replica.lineSpacing = 1.35f * Replica.fontSize / Mathf.Max(1, pitch);
            Replica.rectTransform.Fill();
            Replica.rectTransform.offsetMin = new Vector2(43.5f, 34.5f);
            Replica.rectTransform.offsetMax = new Vector2(-43.5f, -34.5f);
            cursor = Image(Replica.transform, "Cursor", Green).rectTransform;
            direction = Label(speech, "Direction", "", 20, Green, TextAnchor.MiddleLeft);
            direction.gameObject.AddComponent<RadioTextTracking>().Spacing = 3;
            Hint = Label(speech, "Continue", Loc.T("dialog.continue").TrimStart('▶', ' ') + "  ▶", 24, Muted, TextAnchor.MiddleRight);
        }
        public void SetVisible(bool visible)
        {
            backdrop.gameObject.SetActive(visible);
            gameObject.SetActive(visible);
        }
        public void ShowSpeaker(string speaker)
        {
            boss = speaker == "boss";
            var tint = boss ? Amber : Green;
            nameText.text = Loc.T("speaker." + speaker).ToUpperInvariant();
            nameText.color = connection.color = direction.color = lamp.color = cursor.GetComponent<Image>().color = tint;
            foreach (var image in speechBorder) image.color = tint;
            foreach (var image in portraitBorder) image.color = boss ? Hex(0x827247) : Hex(0x597D5D);
            rule.color = boss ? Hex(0x554A2D) : Hex(0x294C36);
            connection.text = Loc.T(boss ? "radio.transmit" : "radio.connected");
            direction.text = receiverDirection.text = Loc.T(boss ? "radio.transmit" : "radio.receive");
            receiverDirection.color = boss ? Amber : Copy;
            receiverImage.sprite = Resources.Load<Sprite>("Sprites/receiver_" + (boss ? "transmit" : "receive"));
            portrait.sprite = boss ? Art.S("portrait_boss") : Resources.Load<Sprite>("Sprites/portrait_major_serious");
            Layout();
        }
        void Layout()
        {
            lastSize = root.rect.size;
            float h = lastSize.y, w = lastSize.x, rowH = h * .32f, recvW = rowH * 680 / 244;
            float rowW = recvW + 48 + 288, portraitH = rowH - 45;
            row.At(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -h * .19f), new Vector2(rowW, rowH));
            Top(heading.rectTransform, 0, h * .123f, w, 51);
            Top(receiver, 0, 0, recvW, rowH);
            receiverDirection.fontSize = Mathf.RoundToInt(27 * rowH / 244);
            Top(portraitFrame, recvW + 48, 0, 288, portraitH);
            if (boss)
            {
                portrait.rectTransform.Fill();
                portrait.rectTransform.offsetMin = new Vector2(12, 0);
                portrait.rectTransform.offsetMax = new Vector2(-12, -12);
            }
            else
                portrait.rectTransform.At(new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, portraitH * .11f), new Vector2(288, 288) * 1.35f);
            // Centre the status group without letting the longer transmission label move the portrait.
            float labelWidth = connection.preferredWidth, groupW = 10.5f + 12 + labelWidth;
            float left = recvW + 48 + (288 - groupW) * .5f;
            Top(lamp.rectTransform, left, portraitH + 15 + 9.75f, 10.5f, 10.5f);
            Top(connection.rectTransform, left + 22.5f, portraitH + 15, labelWidth + 1, 30);
            speech.anchorMin = new Vector2(.1f, 0); speech.anchorMax = new Vector2(.9f, 0);
            speech.pivot = new Vector2(.5f, 0); speech.offsetMin = new Vector2(0, 37.5f); speech.offsetMax = new Vector2(0, 457.5f);
            float speechW = w * .8f;
            Top(nameText.rectTransform, 0, 0, speechW, 48);
            float ruleX = nameText.preferredWidth + 30;
            Top(rule.rectTransform, ruleX, 24, speechW - ruleX, 1.5f);
            Top(window, 0, 66, speechW, 297);
            Top(direction.rectTransform, 0, 385.5f, speechW / 2, 36);
            Top(Hint.rectTransform, speechW / 2, 385.5f, speechW / 2, 36);
        }
        void LateUpdate()
        {
            if (root.rect.size != lastSize) Layout();
            Top(cursor, 0, Replica.preferredHeight + 12, 15, 6);
        }
    }

    /// Tracking for the short one-line headings; body text keeps the font's original metrics.
    public sealed class RadioTextTracking : BaseMeshEffect
    {
        public float Spacing;
        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            var text = GetComponent<Text>();
            int count = helper.currentVertCount / 4;
            float shift = text.alignment == TextAnchor.MiddleCenter ? (count - 1) * Spacing * -.5f : 0;
            for (int i = 0; i < count; i++)
                for (int j = 0; j < 4; j++)
                {
                    var v = new UIVertex(); helper.PopulateUIVertex(ref v, i * 4 + j);
                    v.position.x += shift + i * Spacing; helper.SetUIVertex(v, i * 4 + j);
                }
        }
    }
}
