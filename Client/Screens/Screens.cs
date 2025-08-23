using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Bomberman.Client.UI;
using Bomberman.Client.Net;
using System;

namespace Bomberman.Client.Screens
{
    public interface IScreen
    {
        void OnEnter();
        void Update(GameTime t);
        void Draw(SpriteBatch sb);
        void TextInput(char c);
    }

    public class ScreenManager
    {
        readonly Dictionary<string, IScreen> _screens = new();
        IScreen? _current;
        public void Add(string key, IScreen screen) => _screens[key] = screen;
        public void Show(string key) { _current = _screens[key]; _current.OnEnter(); }
        public void Update(GameTime t) => _current?.Update(t);
        public void Draw(SpriteBatch sb) => _current?.Draw(sb);
        public void TextInput(char c) => _current?.TextInput(c);
    }

    // ---------- Login ----------
    public class LoginScreen : IScreen
    {
        readonly Game1 _game;
        readonly Label title = new(){ Text="Bomberman - Login" };
        readonly Label err = new(){ Text="" , Color=new Color(255,140,140)};
        readonly TextBox user = new(){ Placeholder="username" };
        readonly TextBox pass = new(){ Placeholder="password" };
        readonly Button loginBtn = new(){ Text="Login" };
        bool busy;

        public LoginScreen(Game1 g)
        {
            _game = g;
            loginBtn.OnClick = async () => {
                if (busy) return;
                busy = true; err.Text = "";
                try {
                    await _game.Api.LoginAsync(user.Text, pass.Text.Length>0?pass.Text:"x");
                    _game.Screens.Show("menu");
                } catch (Exception ex) { err.Text = ex.Message; }
                busy = false;
            };
        }
        public void OnEnter() { user.Focused = true; }
        public void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            var layout = new Layout(new Rectangle(120,100, 400, 40));
            title.Bounds = new Rectangle(120, 40, 500, 40);
            user.Bounds = layout.Next(40);
            pass.Bounds = layout.Next(40);
            loginBtn.Bounds = layout.Next(44);
            err.Bounds = layout.Next(40);

            user.Update(t,m,k); pass.Update(t,m,k); loginBtn.Update(t,m,k);
            if (k.IsKeyDown(Keys.Tab)) { user.Focused = !user.Focused; pass.Focused = !user.Focused; }
            if (k.IsKeyDown(Keys.Enter)) loginBtn.OnClick?.Invoke();
        }
        public void Draw(SpriteBatch sb)
        {
            sb.DrawString(Ui.Font, title.Text, new Vector2(title.Bounds.X, title.Bounds.Y), Color.White);
            user.Draw(sb); pass.Draw(sb); loginBtn.Draw(sb);
            err.Draw(sb);
        }
        public void TextInput(char c) { user.TextInput(c); pass.TextInput(c); }
    }

    // ---------- Main Menu ----------
    public class MenuScreen : IScreen
    {
        readonly Game1 _game;
        readonly Label hello = new();
        readonly Button leaderboard = new(){ Text="Leaderboard" };
        readonly Button createLobby = new(){ Text="Create Lobby (2)" };
        readonly TextBox lobbyId = new(){ Placeholder="Lobby ID or Code" };
        readonly Button joinLobby = new(){ Text="Join Lobby" };
        readonly Button ws = new(){ Text="WebSocket Tester" };
        readonly Button quit = new(){ Text="Quit" };
        string info = "";

        public MenuScreen(Game1 g)
        {
            _game = g;
            leaderboard.OnClick = () => _game.Screens.Show("leaderboard");
            createLobby.OnClick = async () => {
                var lobby = await _game.Api.CreateLobbyAsync(2);
                // Auto-join and switch to lobby screen
                var joined = await _game.Api.JoinLobbyAsync(lobby.LobbyId);
                var code = await _game.Api.GetLobbyCodeAsync(joined.LobbyId);
                _game.LobbyScreenRef.SetLobby(joined, code);
                _game.Screens.Show("lobby");
            };
            joinLobby.OnClick = async () => {
                try {
                    var l = await _game.Api.JoinLobbyAsync(lobbyId.Text.Trim());
                    var code = await _game.Api.GetLobbyCodeAsync(l.LobbyId);
                    _game.LobbyScreenRef.SetLobby(l, code);
                    _game.Screens.Show("lobby");
                } catch(Exception ex) { info = ex.Message; }
            };
            ws.OnClick = () => _game.Screens.Show("ws");
            quit.OnClick = () => _game.Exit();
        }
        public void OnEnter() { }
        public void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            hello.Text = _game.Api.Me != null ? $"Hello, {_game.Api.Me.Username} ({_game.Api.Me.Id})" : "Hello";
            var layout = new Layout(new Rectangle(120,90, 420, 44));
            hello.Bounds = new Rectangle(120, 40, 700, 40);
            leaderboard.Bounds = layout.Next(44);
            createLobby.Bounds = layout.Next(44);
            lobbyId.Bounds = layout.Next(40);
            joinLobby.Bounds = layout.Next(44);
            ws.Bounds = layout.Next(44);
            quit.Bounds = layout.Next(44);

            lobbyId.Update(t,m,k);
            leaderboard.Update(t,m,k); createLobby.Update(t,m,k); joinLobby.Update(t,m,k); ws.Update(t,m,k); quit.Update(t,m,k);
            if (k.IsKeyDown(Keys.Escape)) _game.Exit();
        }
        public void Draw(SpriteBatch sb)
        {
            sb.DrawString(Ui.Font, hello.Text, new Vector2(hello.Bounds.X, hello.Bounds.Y), Color.White);
            leaderboard.Draw(sb); createLobby.Draw(sb); lobbyId.Draw(sb); joinLobby.Draw(sb); ws.Draw(sb); quit.Draw(sb);
            if (!string.IsNullOrEmpty(info))
                sb.DrawString(Ui.Font, info, new Vector2(120, 420), new Color(200,220,255));
        }
        public void TextInput(char c) { lobbyId.TextInput(c); }
    }

    // ---------- Leaderboard ----------
    public class LeaderboardScreen : IScreen
    {
        readonly Game1 _game;
        readonly Button back = new(){ Text="Back" };
        readonly Button prev = new(){ Text="Prev" };
        readonly Button next = new(){ Text="Next" };
        int page = 1; const int pageSize = 10; int totalPages = 1;
        List<User> users = new();

        public LeaderboardScreen(Game1 g)
        {
            _game = g;
            back.OnClick = () => _game.Screens.Show("menu");
            prev.OnClick = async () => { if (page>1){ page--; await Fetch(); } };
            next.OnClick = async () => { if (page<totalPages){ page++; await Fetch(); } };
        }
        public async void OnEnter(){ await Fetch(); }
        async System.Threading.Tasks.Task Fetch()
        {
            var res = await _game.Api.GetLeaderboardAsync(page, pageSize);
            users = res.Users;
            totalPages = res.TotalPages;
            page = res.Page;
        }
        public void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            back.Bounds = new Rectangle(30,30,100,40);
            prev.Bounds = new Rectangle(120,400,100,40);
            next.Bounds = new Rectangle(230,400,100,40);
            back.Update(t,m,k); prev.Update(t,m,k); next.Update(t,m,k);
            if (k.IsKeyDown(Keys.Escape)) _game.Screens.Show("menu");
        }
        public void Draw(SpriteBatch sb)
        {
            sb.DrawString(Ui.Font, "Leaderboard", new Vector2(150, 40), Color.White);
            int y = 100;
            for (int i=0;i<users.Count;i++)
            {
                var u = users[i];
                sb.DrawString(Ui.Font, $"{(i+1)+(page-1)*10}. {u.Username}  -  score {u.Stats.TotalScore}", new Vector2(120,y), Color.White);
                y += Ui.Font.LineSpacing + 6;
            }
            sb.DrawString(Ui.Font, $"Page {page}/{totalPages}", new Vector2(120, 370), new Color(200,220,255));
            back.Draw(sb); prev.Draw(sb); next.Draw(sb);
        }
        public void TextInput(char c) { }
    }

    // ---------- In-Lobby screen ----------
    public class LobbyScreen : IScreen
    {
        readonly Game1 _game;
        readonly Button back = new(){ Text="Leave Lobby" };
        Lobby? _lobby;
        string _code = "";
        double _poll;

        public LobbyScreen(Game1 g)
        {
            _game = g;
            back.OnClick = () => { _game.Screens.Show("menu"); };
        }
        public void SetLobby(Lobby lobby, string code) { _lobby = lobby; _code = code; }
        public void OnEnter() { _poll = 0; }
        public async void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            back.Bounds = new Rectangle(30,30,140,40);
            back.Update(t,m,k);
            if (k.IsKeyDown(Keys.Escape)) _game.Screens.Show("menu");
            // poll lobby state every 2s
            _poll += t.ElapsedGameTime.TotalSeconds;
            if (_poll >= 2.0 && _lobby != null)
            {
                _poll = 0;
                try { _lobby = await _game.Api.GetLobbyAsync(_lobby.LobbyId); } catch { /* ignore transient */ }
            }
        }
        public void Draw(SpriteBatch sb)
        {
            if (_lobby == null)
            {
                sb.DrawString(Ui.Font, "No lobby.", new Vector2(120, 120), Color.White);
                back.Draw(sb);
                return;
            }
            // Big, shareable code box
            var header = "Lobby Code";
            sb.DrawString(Ui.Font, header, new Vector2(120, 60), new Color(200,220,255));
            var box = new Rectangle(120, 90, 300, 80);
            sb.DrawRect(box, new Color(35,45,70));
            sb.DrawFrame(box, new Color(20,25,35), 3);
            // scale the code
            var measure = Ui.Font.MeasureString(_code);
            var scale = Math.Min( (box.Width-20) / Math.Max(1f, measure.X), (box.Height-20) / Math.Max(1f, measure.Y) );
            sb.DrawString(Ui.Font, _code, new Vector2(box.X + 10, box.Y + 10), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Details and players
            sb.DrawString(Ui.Font, $"Lobby: {_lobby.LobbyId}", new Vector2(120, 190), new Color(180,200,240));
            sb.DrawString(Ui.Font, $"Status: {_lobby.Status}", new Vector2(120, 220), new Color(180,200,240));
            sb.DrawString(Ui.Font, "Players:", new Vector2(120, 260), Color.White);
            int y = 290;
            for (int i=0;i<_lobby.Players.Count;i++)
            {
                var u = _lobby.Players[i];
                sb.DrawString(Ui.Font, $"{i+1}. {u.Username}", new Vector2(140, y), Color.White);
                y += Ui.Font.LineSpacing + 4;
            }

            back.Draw(sb);
        }
        public void TextInput(char c) { }
    }

    // ---------- WebSocket tester ----------
    public class WsScreen : IScreen
    {
        readonly Game1 _game;
        readonly TextBox gameId = new(){ Placeholder="gameId" };
        readonly TextBox move = new(){ Placeholder="move: up/down/left/right/bomb/stay" };
        readonly Button send = new(){ Text="Send Move" };
        readonly Button back = new(){ Text="Back" };
        string info = "";

        public WsScreen(Game1 g)
        {
            _game = g;
            send.OnClick = async () => {
                try {
                    if (_game.Socket == null)
                    {
                        _game.Socket = new GameSocket();
                        await _game.Socket.ConnectAsync(new Uri(_game.WsUrl));
                    }
                    await _game.Socket.SendMoveAsync(gameId.Text, _game.Api.Me!.Id, move.Text);
                    var (state, error) = await _game.Socket.ReceiveAsync();

                    if (error != null)
                    {
                        info = $"{error.ErrorCode}: {error.ErrorMessage}";
                    }
                    else
                    {
                        string board00 =
                            (state != null && state.Board != null &&
                             state.Board.Length > 0 && state.Board[0] != null &&
                             state.Board[0].Length > 0)
                            ? state.Board[0][0].ToString()
                            : "n/a";

                        info = $"OK board00={board00}";
                    }
                } catch(Exception ex) { info = ex.Message; }
            };
            back.OnClick = () => _game.Screens.Show("menu");
        }
        public void OnEnter(){}
        public void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            var layout = new Layout(new Rectangle(120,90, 520, 44));
            gameId.Bounds = layout.Next(40);
            move.Bounds = layout.Next(40);
            send.Bounds = layout.Next(44);
            back.Bounds = layout.Next(44);
            gameId.Update(t,m,k); move.Update(t,m,k); send.Update(t,m,k); back.Update(t,m,k);
            if (k.IsKeyDown(Keys.Escape)) _game.Screens.Show("menu");
        }
        public void Draw(SpriteBatch sb)
        {
            sb.DrawString(Ui.Font, "WebSocket Tester", new Vector2(120, 40), Color.White);
            gameId.Draw(sb); move.Draw(sb); send.Draw(sb); back.Draw(sb);
            if (!string.IsNullOrEmpty(info))
                sb.DrawString(Ui.Font, info, new Vector2(120, 360), new Color(200,220,255));
        }
        public void TextInput(char c) { gameId.TextInput(c); move.TextInput(c); }
    }
}
