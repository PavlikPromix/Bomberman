using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.Client.Net;
using Bomberman.Client.UI;
using System;

namespace Bomberman.Client.Screens
{
    public class GameplayScreen : IScreen
    {
        readonly Game1 _game;
        readonly Button back = new(){ Text="Back" };

        public string GameId = "";
        public string LobbyId = "";
        GameState? _state;

        const int Cell = 24;     // 25*24 = 600px tall
        const int OriginX = 100; // grid left
        const int OriginY = 0;   // grid top

        bool _up,_down,_left,_right,_space;

        public GameplayScreen(Game1 g) { _game = g; back.OnClick = () => _game.Screens.Show("menu"); }

        public void SetGame(string lobbyId, string gameId, GameState? initial = null)
        { LobbyId = lobbyId; GameId = gameId; _state = initial; }

        public async void OnEnter()
        {
            try
            {
                var latest = await _game.Api.GetActiveGameByLobbyAsync(LobbyId);
                if (IsValid(latest)) _state = latest;
            } catch { }

            if (_game.Socket == null)
            {
                _game.Socket = new GameSocket();
                await _game.Socket.ConnectAsync(new Uri(_game.WsUrl));
            }
            _game.Socket.StartListening(
                onState: (s) => { if (IsValid(s)) _state = s; },
                onError: (e) => { /* optionally surface */ }
            );
            await _game.Socket.SendMoveAsync(GameId, _game.Api.Me!.Id, "noop"); // register & get echo state
        }

        bool IsValid(GameState? s) =>
            s != null && s.Board != null && s.Board.Length > 0 && s.Board[0] != null && s.Board[0].Length > 0;

        public async void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            back.Bounds = new Rectangle(20, 20, 100, 36);
            back.Update(t,m,k);

            bool dUp = k.IsKeyDown(Keys.Up) || k.IsKeyDown(Keys.W);
            bool dDown = k.IsKeyDown(Keys.Down) || k.IsKeyDown(Keys.S);
            bool dLeft = k.IsKeyDown(Keys.Left) || k.IsKeyDown(Keys.A);
            bool dRight = k.IsKeyDown(Keys.Right) || k.IsKeyDown(Keys.D);
            bool dSpace = k.IsKeyDown(Keys.Space);

            async System.Threading.Tasks.Task send(string mv) =>
                await _game.Socket!.SendMoveAsync(GameId, _game.Api.Me!.Id, mv);

            if (dUp && !_up) await send("up");
            if (dDown && !_down) await send("down");
            if (dLeft && !_left) await send("left");
            if (dRight && !_right) await send("right");
            if (dSpace && !_space) await send("bomb");

            _up=dUp; _down=dDown; _left=dLeft; _right=dRight; _space=dSpace;
        }

        public void Draw(SpriteBatch sb)
        {
            sb.DrawString(Ui.Font, "Gameplay", new Vector2(140, 8), Color.White);
            back.Draw(sb);

            // Use 25x25 fallback if state is missing/empty
            int rows = 25, cols = 25;
            if (IsValid(_state)) { rows = _state!.Board.Length; cols = _state!.Board[0].Length; }

            // grid
            for (int y=0;y<=rows;y++)
                sb.DrawRect(new Rectangle(OriginX, OriginY + y*Cell, cols*Cell, 1), new Color(35,45,75));
            for (int x=0;x<=cols;x++)
                sb.DrawRect(new Rectangle(OriginX + x*Cell, OriginY, 1, rows*Cell), new Color(35,45,75));

            if (!IsValid(_state)) return;

            // board
            for (int y=0;y<rows;y++)
            {
                var row = _state!.Board[y];
                for (int x=0;x<cols;x++)
                {
                    int v = row[x];
                    var r = new Rectangle(OriginX + x*Cell, OriginY + y*Cell, Cell, Cell);

                    if (v == 1) sb.DrawRect(r, new Color(10,15,25));               // solid wall
                    else if (v == 2) sb.DrawRect(r.Pad(3), new Color(240,210,120)); // crate
                    else if (v == 20) DrawCircle(sb, r.Center, Cell/3, new Color(240,240,250)); // bomb
                    else if (v == 30) sb.DrawRect(r.Pad(2), new Color(255,120,60)); // flame
                    else if (v >= 11 && v < 20) DrawTriangle(sb, r, v);            // player
                }
            }

            // debug text inside the window
            int a = _state!.Board[1][1];
            int b = _state!.Board[rows-2][cols-2];
            sb.DrawString(Ui.Font, $"b[1,1]={a}  b[end,end]={b}", new Vector2(OriginX + 4, OriginY + rows*Cell - 18), new Color(200, 240, 255));
        }

        public void TextInput(char c) { }

        private void DrawTriangle(SpriteBatch sb, Rectangle r, int code)
        {
            var p1 = new Vector2(r.X + r.Width/2f, r.Y + 3);
            var p2 = new Vector2(r.X + 3, r.Bottom - 3);
            var p3 = new Vector2(r.Right - 3, r.Bottom - 3);
            var col = (code==11) ? new Color(120,220,255) : new Color(255,200,120);
            FillTri(sb, p1,p2,p3, col);
        }

        private void DrawCircle(SpriteBatch sb, Point center, int radius, Color col)
        {
            for (int yy=-radius; yy<=radius; yy++)
            for (int xx=-radius; xx<=radius; xx++)
                if (xx*xx + yy*yy <= radius*radius)
                    sb.DrawRect(new Rectangle(center.X+xx, center.Y+yy, 1, 1), col);
        }

        private void FillTri(SpriteBatch sb, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            float minX = MathF.Min(a.X, MathF.Min(b.X, c.X));
            float maxX = MathF.Max(a.X, MathF.Max(b.X, c.X));
            float minY = MathF.Min(a.Y, MathF.Min(b.Y, c.Y));
            float maxY = MathF.Max(a.Y, MathF.Max(b.Y, c.Y));
            for (int y=(int)minY; y<=maxY; y++)
            for (int x=(int)minX; x<=maxX; x++)
            {
                var p = new Vector2(x+0.5f,y+0.5f);
                if (InsideTri(p,a,b,c))
                    sb.DrawRect(new Rectangle(x,y,1,1), col);
            }
        }
        private float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X)*(c.Y - a.Y) - (b.Y - a.Y)*(c.X - a.X);
        private bool InsideTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            bool b1 = Cross(p,a,b) < 0.0f;
            bool b2 = Cross(p,b,c) < 0.0f;
            bool b3 = Cross(p,c,a) < 0.0f;
            return (b1 == b2) && (b2 == b3);
        }
    }
}
