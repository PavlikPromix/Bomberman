using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.Client.UI;

namespace Bomberman.Client.Screens
{
    public class FinalScreen : IScreen
    {
        readonly Game1 _game;
        string _winner = "Unknown";
        readonly Button back = new(){ Text="Back to Menu" };

        public FinalScreen(Game1 g)
        {
            _game = g;
            back.OnClick = () => _game.Screens.Show("menu");
        }

        public void SetResult(string winner) { _winner = winner; }

        public void OnEnter() { }
        public void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            back.Bounds = new Rectangle(380, 520, 200, 52);
            back.Update(t,m,k);
        }
        public void Draw(SpriteBatch sb)
        {
            sb.DrawString(Ui.Font, "Game Over", new Vector2(380, 140), Color.White);
            sb.DrawString(Ui.Font, $"Winner: {_winner}", new Vector2(340, 220), new Color(200,220,255));
            back.Draw(sb);
        }
        public void TextInput(char c) { }
    }
}
