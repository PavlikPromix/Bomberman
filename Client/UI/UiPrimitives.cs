using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;

namespace Bomberman.Client.UI
{
    public static class Ui
    {
        public static SpriteFont Font = null!;
        public static Texture2D Pixel = null!; // 1x1 white

        public static Rectangle Pad(this Rectangle r, int p) =>
            new Rectangle(r.X + p, r.Y + p, r.Width - p*2, r.Height - p*2);

        public static void DrawRect(this SpriteBatch sb, Rectangle r, Color c) => sb.Draw(Pixel, r, c);

        public static void DrawFrame(this SpriteBatch sb, Rectangle r, Color c, int thickness = 2)
        {
            sb.DrawRect(new Rectangle(r.X, r.Y, r.Width, thickness), c);
            sb.DrawRect(new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), c);
            sb.DrawRect(new Rectangle(r.X, r.Y, thickness, r.Height), c);
            sb.DrawRect(new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), c);
        }
    }

    public abstract class Widget
    {
        public Rectangle Bounds;
        public bool Visible = true;
        public virtual void Update(GameTime t, MouseState m, KeyboardState k) { }
        public virtual void TextInput(char c) { }
        public virtual void Draw(SpriteBatch sb) { }
        public bool Contains(Point p) => Bounds.Contains(p);
    }

    public class Label : Widget
    {
        public string Text = "";
        public Color Color = Color.White;
        public override void Draw(SpriteBatch sb)
        {
            if (!Visible) return;
            sb.DrawString(Ui.Font, Text, new Vector2(Bounds.X, Bounds.Y), Color);
        }
    }

    public class Button : Widget
    {
        public string Text = "Button";
        public Action? OnClick;
        bool _wasDown;
        public override void Update(GameTime t, MouseState m, KeyboardState k)
        {
            var over = Visible && Contains(m.Position);
            var nowDown = over && m.LeftButton == ButtonState.Pressed;
            if (_wasDown && !nowDown && over) OnClick?.Invoke();
            _wasDown = nowDown;
        }
        public override void Draw(SpriteBatch sb)
        {
            if (!Visible) return;
            var over = Contains(Mouse.GetState().Position);
            sb.DrawRect(Bounds, over ? new Color(70,90,120) : new Color(55,70,100));
            sb.DrawFrame(Bounds, new Color(20,25,35));
            var size = Ui.Font.MeasureString(Text);
            var pos = new Vector2(Bounds.X + (Bounds.Width - size.X)/2, Bounds.Y + (Bounds.Height - size.Y)/2);
            sb.DrawString(Ui.Font, Text, pos, Color.White);
        }
    }

    public class TextBox : Widget
    {
        public string Text = "";
        public string Placeholder = "";
        public bool Focused;

        // Proper backspace handling (edge + repeat)
        bool _backHeld;
        double _backTimer;
        const double InitialDelay = 0.35; // seconds before repeat starts
        const double RepeatRate   = 0.05; // seconds between repeats

        double caretBlink;

        public override void Update(GameTime t, MouseState m, KeyboardState k)
        {
            if (!Visible) return;

            // Focus on click
            if (m.LeftButton == ButtonState.Pressed)
                Focused = Contains(m.Position);

            // Backspace edge + repeat timing
            var backDown = Focused && k.IsKeyDown(Keys.Back);
            if (backDown && !_backHeld)
            {
                DeleteOne();
                _backTimer = InitialDelay;
            }
            else if (backDown && _backHeld)
            {
                _backTimer -= t.ElapsedGameTime.TotalSeconds;
                if (_backTimer <= 0)
                {
                    DeleteOne();
                    _backTimer = RepeatRate;
                }
            }
            else if (!backDown && _backHeld)
            {
                _backTimer = 0;
            }
            _backHeld = backDown;

            caretBlink += t.ElapsedGameTime.TotalSeconds;
        }

        private void DeleteOne()
        {
            if (Text.Length > 0) Text = Text.Substring(0, Text.Length - 1);
        }

        public override void TextInput(char c)
        {
            if (!Focused) return;
            if (c == '\r' || c == '\n' || c == '\b') return; // ignore enter/backspace from TextInput
            if (!char.IsControl(c)) Text += c;
        }

        public override void Draw(SpriteBatch sb)
        {
            if (!Visible) return;
            sb.DrawRect(Bounds, new Color(35,45,70));
            sb.DrawFrame(Bounds, new Color(20,25,35));
            var drawText = Text.Length > 0 ? Text : Placeholder;
            var color = Text.Length > 0 ? Color.White : new Color(170,170,170);
            var pos = new Vector2(Bounds.X + 8, Bounds.Y + 6);
            sb.DrawString(Ui.Font, drawText, pos, color);

            if (Focused && ((int)(caretBlink*2)%2)==0)
            {
                var size = Ui.Font.MeasureString(Text);
                var x = (int)(pos.X + size.X + 2);
                sb.DrawRect(new Rectangle(x, Bounds.Y + 6, 2, Ui.Font.LineSpacing-6), Color.White);
            }
        }
    }

    public class Layout
    {
        public Rectangle Area;
        int _cursorY;
        int _gap;
        public Layout(Rectangle area, int gap = 10) { Area = area; _cursorY = area.Y; _gap = gap; }
        public Rectangle Next(int height) { var r = new Rectangle(Area.X, _cursorY, Area.Width, height); _cursorY += height + _gap; return r; }
    }
}
