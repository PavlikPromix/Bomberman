using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace Bomberman.Client.UI
{
    public static class Ui
    {
        public static Texture2D Pixel = null!;
        public static SpriteFont Font = null!;

        /// <summary>Return only printable ASCII (32..126). Drops everything else (e.g., Cyrillic).</summary>
        public static string SafeAscii(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var arr = new char[s.Length];
            int j = 0;
            foreach (var ch in s)
                if (ch >= ' ' && ch <= '~') arr[j++] = ch;
            return new string(arr, 0, j);
        }

        /// <summary>Draws text safely: strips non-ASCII and swallows any unexpected font error.</summary>
        public static void SafeDrawString(this SpriteBatch sb, string text, Vector2 pos, Color color, float scale = 1f)
        {
            var clean = SafeAscii(text ?? "");
            if (clean.Length == 0) return;
            try { sb.DrawString(Font, clean, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f); }
            catch { /* never crash the UI */ }
        }

        public static void DrawRect(this SpriteBatch sb, Rectangle r, Color c) => sb.Draw(Pixel, r, c);

        public static void DrawFrame(this SpriteBatch sb, Rectangle r, Color c, int thickness = 2)
        {
            sb.DrawRect(new Rectangle(r.X, r.Y, r.Width, thickness), c);
            sb.DrawRect(new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), c);
            sb.DrawRect(new Rectangle(r.X, r.Y, thickness, r.Height), c);
            sb.DrawRect(new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), c);
        }

        public static Rectangle Pad(this Rectangle r, int p) =>
            new Rectangle(r.X + p, r.Y + p, Math.Max(0, r.Width - 2 * p), Math.Max(0, r.Height - 2 * p));
    }

    public class Layout
    {
        private Rectangle _r;
        private readonly int _gap;
        public Layout(Rectangle r, int gap) { _r = r; _gap = gap; }
        public Rectangle Next(int h)
        {
            var outR = new Rectangle(_r.X, _r.Y, _r.Width, h);
            _r.Y += h + _gap;
            return outR;
        }
    }

    public class Label
    {
        public string Text { get; set; } = "";
        public Color Color { get; set; } = Color.White;
        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; } = true;

        public void Draw(SpriteBatch sb)
        {
            if (!Visible) return;
            sb.SafeDrawString(Text, new Vector2(Bounds.X, Bounds.Y), Color);
        }
    }

    public class Button
    {
        public string Text { get; set; } = "Button";
        public Rectangle Bounds { get; set; }
        public bool Visible { get; set; } = true;
        public Action? OnClick { get; set; }
        private bool _pressed = false;

        public void Update(GameTime t, MouseState m, KeyboardState k)
        {
            if (!Visible) return;
            var inside = Bounds.Contains(m.Position);
            bool down = m.LeftButton == ButtonState.Pressed;
            if (down && inside && !_pressed) _pressed = true;
            if (!down && _pressed)
            {
                if (inside) OnClick?.Invoke();
                _pressed = false;
            }
        }

        public void Draw(SpriteBatch sb)
        {
            if (!Visible) return;
            sb.DrawRect(Bounds, new Color(60, 60, 60));
            sb.DrawFrame(Bounds, new Color(70, 70, 70), 2);
            var txt = Ui.SafeAscii(Text);
            var size = Ui.Font.MeasureString(txt);
            var pos = new Vector2(Bounds.X + (Bounds.Width - size.X) / 2f, Bounds.Y + (Bounds.Height - size.Y) / 2f);
            sb.SafeDrawString(txt, pos, Color.White);
        }
    }

    public class TextBox
    {
        public string Text { get; set; } = "";
        public string Placeholder { get; set; } = "";
        public Rectangle Bounds { get; set; }
        public bool Focused { get; set; } = false;
        public bool Visible { get; set; } = true;

        // Caret/blink
        private double _blinkTimer = 0;
        private bool _caretOn = true;

        public void Update(GameTime t, MouseState m, KeyboardState k)
        {
            if (!Visible) return;

            if (m.LeftButton == ButtonState.Pressed)
                Focused = Bounds.Contains(m.Position);

            // Blink only when focused
            if (Focused)
            {
                _blinkTimer += t.ElapsedGameTime.TotalSeconds;
                if (_blinkTimer >= 0.5) { _blinkTimer = 0; _caretOn = !_caretOn; }
            }
            else
            {
                _blinkTimer = 0;
                _caretOn = false;
            }
        }

        /// <summary>
        /// Accept only printable ASCII (32..126). Ignore anything else (e.g., Cyrillic) to prevent crashes.
        /// Backspace and Enter come as TextInput chars (\b and \r).
        /// </summary>
        public void TextInput(char c)
        {
            if (!Focused || !Visible) return;

            // Backspace arrives as \b
            if (c == '\b')
            {
                if (Text.Length > 0) Text = Text.Substring(0, Text.Length - 1);
                // make caret visible immediately after edit
                _caretOn = true; _blinkTimer = 0;
                return;
            }

            // Ignore non-ASCII / control chars
            if (c < ' ' || c > '~') return;

            Text += c;
            // reset blink so caret shows after typing
            _caretOn = true; _blinkTimer = 0;
        }

        public void Draw(SpriteBatch sb)
        {
            if (!Visible) return;
            var bg = new Color(40, 40, 40);
            var br = new Color(70, 70, 70);
            sb.DrawRect(Bounds, bg);
            sb.DrawFrame(Bounds, br, 2);

            var asciiText = Ui.SafeAscii(Text);
            bool showPlaceholder = asciiText.Length == 0 && !Focused;
            string display = showPlaceholder ? Ui.SafeAscii(Placeholder) : asciiText;
            var col = showPlaceholder ? new Color(150, 150, 150) : Color.White;

            // Text clipping (left-trim to keep caret at the right edge when overflowing)
            var maxW = Bounds.Width - 12;
            string clipped = display;
            while (clipped.Length > 0 && Ui.Font.MeasureString(clipped).X > maxW)
                clipped = clipped.Substring(1);

            // Draw text
            var textY = Bounds.Y + (Bounds.Height - Ui.Font.LineSpacing) / 2f;
            sb.SafeDrawString(clipped, new Vector2(Bounds.X + 6, textY), col);

            // Draw caret (only when focused and not showing placeholder)
            if (Focused)
            {
                float caretX;
                if (asciiText.Length == 0)
                {
                    caretX = Bounds.X + 6; // at start when empty
                }
                else
                {
                    // caret sits after the (possibly clipped) visible text; keep inside box
                    var fullWidth = Ui.Font.MeasureString(asciiText).X;
                    caretX = Bounds.X + 6 + Math.Min(fullWidth, maxW);
                }

                if (_caretOn)
                {
                    int caretH = Math.Min(Ui.Font.LineSpacing, Bounds.Height - 6);
                    var caretRect = new Rectangle((int)caretX, Bounds.Y + (Bounds.Height - caretH) / 2, 2, caretH);
                    sb.DrawRect(caretRect, new Color(220, 220, 220));
                }
            }
        }
    }
}
