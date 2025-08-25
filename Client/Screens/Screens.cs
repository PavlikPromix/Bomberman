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
        readonly Label err = new(){ Text="" , Color=new Color(255,120,120)};
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
                    await _game.Api.LoginAsync(user.Text, pass.Text);
                    _game.Screens.Show("menu");
                } catch (Exception ex) { err.Text = ex.Message; }
                busy = false;
            };
        }
        public void OnEnter() { user.Focused = true; }
        public void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            var layout = new Layout(new Rectangle(140,120, 460, 44), 14);
            title.Bounds = new Rectangle(140, 60, 500, 44);
            user.Bounds = layout.Next(44);
            pass.Bounds = layout.Next(44);
            loginBtn.Bounds = layout.Next(48);
            err.Bounds = layout.Next(44);

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
        readonly Button quit = new(){ Text="Quit" };
        string info = "";

        public MenuScreen(Game1 g)
        {
            _game = g;
            leaderboard.OnClick = () => _game.Screens.Show("leaderboard");
            createLobby.OnClick = async () => {
                var lobby = await _game.Api.CreateLobbyAsync(2);
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
            quit.OnClick = () => _game.Exit();
        }
        public void OnEnter() { }
        public void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            hello.Text = _game.Api.Me != null ? $"Hello, {_game.Api.Me.Username}" : "Hello";
            var layout = new Layout(new Rectangle(140,120, 460, 48), 14);
            hello.Bounds = new Rectangle(140, 70, 700, 44);
            leaderboard.Bounds = layout.Next(48);
            createLobby.Bounds = layout.Next(48);
            lobbyId.Bounds = layout.Next(44);
            joinLobby.Bounds = layout.Next(48);
            quit.Bounds = layout.Next(48);

            lobbyId.Update(t,m,k);
            leaderboard.Update(t,m,k); createLobby.Update(t,m,k); joinLobby.Update(t,m,k); quit.Update(t,m,k);
            if (k.IsKeyDown(Keys.Escape)) _game.Exit();
        }
        public void Draw(SpriteBatch sb)
        {
            sb.DrawString(Ui.Font, hello.Text, new Vector2(hello.Bounds.X, hello.Bounds.Y), Color.White);
            leaderboard.Draw(sb); createLobby.Draw(sb); lobbyId.Draw(sb); joinLobby.Draw(sb); quit.Draw(sb);
            if (!string.IsNullOrEmpty(info))
                sb.DrawString(Ui.Font, info, new Vector2(140, 520), new Color(200,220,255));
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
            prev.Bounds = new Rectangle(140,560,100,40);
            next.Bounds = new Rectangle(260,560,100,40);
            back.Update(t,m,k); prev.Update(t,m,k); next.Update(t,m,k);
            if (k.IsKeyDown(Keys.Escape)) _game.Screens.Show("menu");
        }
        public void Draw(SpriteBatch sb)
        {
            sb.DrawString(Ui.Font, "Leaderboard", new Vector2(160, 60), Color.White);
            int y = 120;
            for (int i=0;i<users.Count;i++)
            {
                var u = users[i];
                sb.DrawString(Ui.Font, $"{(i+1)+(page-1)*10}. {u.Username}  -  score {u.Stats.TotalScore}", new Vector2(140,y), Color.White);
                y += Ui.Font.LineSpacing + 8;
            }
            sb.DrawString(Ui.Font, $"Page {page}/{totalPages}", new Vector2(140, 520), new Color(200,220,255));
            back.Draw(sb); prev.Draw(sb); next.Draw(sb);
        }
        public void TextInput(char c) { }
    }

    // ---------- In-Lobby (leader can set rounds/bomb limit) ----------
    public class LobbyScreen : IScreen
    {
        readonly Game1 _game;
        readonly Button back = new(){ Text="Leave Lobby" };
        readonly Button start = new(){ Text="Start (Leader)" };
        readonly Label settingsTitle = new(){ Text="Settings (leader only)" , Color=new Color(200,220,255)};
        readonly Label roundsLabel = new(){ Text="Rounds to win" , Color=new Color(180,200,240)};
        readonly Label bombsLabel  = new(){ Text="Bomb limit" ,   Color=new Color(180,200,240)};
        readonly TextBox rounds = new(){ Placeholder="Rounds to win" };
        readonly TextBox limit = new(){ Placeholder="Bomb limit" };
        Lobby? _lobby;
        string _code = "";
        double _poll;
        string _info = ""; bool _settingsFetched = false;

        public LobbyScreen(Game1 g)
        {
            _game = g;
            back.OnClick = () => { _game.Screens.Show("menu"); };
            start.OnClick = async () =>
            {
                if (_lobby == null) return;
                try
                {
                    // Leader may have typed settings
                    if (_game.Api.Me != null && _lobby.Players.Count>0 && _lobby.Players[0].Id == _game.Api.Me.Id)
                    {
                        int r = ParseOr(rounds.Text, 5);
                        int b = ParseOr(limit.Text, 3);
                        await _game.Api.SetLobbySettingsAsync(_lobby.LobbyId, r, b);
                    }
                    var gs = await _game.Api.StartLobbyAsync(_lobby.LobbyId);
                    _info = $"Started game: {gs.GameId}";
                    _lobby.Status = "in-progress";
                    _game.GameplayRef.SetGame(_lobby.LobbyId, gs.GameId, gs);
                    _game.Screens.Show("gameplay");
                }
                catch (Exception ex) { _info = ex.Message; }
            };
        }
        int ParseOr(string s, int d){ if(int.TryParse(s, out var v)) return v; return d; }

        public void SetLobby(Lobby lobby, string code) { _lobby = lobby; _code = code; _info = ""; }
        public void OnEnter() { _poll = 0; }
        public async void Update(GameTime t)
        {
            var m = Mouse.GetState(); var k = Keyboard.GetState();
            back.Bounds = new Rectangle(30,30,180,40);
            start.Bounds = new Rectangle(620, 320, 260, 48);
            back.Update(t,m,k);

            // Poll lobby state every 0.5s
            _poll += t.ElapsedGameTime.TotalSeconds;
            if (_poll >= 0.5 && _lobby != null)
            {
                _poll = 0;
                try
                {
                    _lobby = await _game.Api.GetLobbyAsync(_lobby.LobbyId);
                    if (!string.Equals(_lobby.Status, "waiting", StringComparison.OrdinalIgnoreCase))
                    {
                        var gs = await _game.Api.GetActiveGameByLobbyAsync(_lobby.LobbyId);
                        _game.GameplayRef.SetGame(_lobby.LobbyId, gs.GameId, gs);
                        _game.Screens.Show("gameplay");
                        return;
                    }
                } catch { }
            }

            bool isLeader = _lobby != null && _game.Api.Me != null && _lobby.Players.Count > 0 && _lobby.Players[0].Id == _game.Api.Me.Id;
            start.Visible = isLeader && _lobby != null && _lobby.Players.Count >= 2 && string.Equals(_lobby.Status, "waiting", StringComparison.OrdinalIgnoreCase);
            if (start.Visible) start.Update(t,m,k);
            // Load settings once from server
            if (!_settingsFetched && _lobby != null) {
                try {
                    var s = await _game.Api.GetLobbySettingsAsync(_lobby.LobbyId);
                    rounds.Text = s.RoundsToWin.ToString();
                    limit.Text  = s.BombLimit.ToString();
                    _settingsFetched = true;
                } catch { }
            }

            // layout for settings area
            settingsTitle.Visible = isLeader;
            roundsLabel.Visible = isLeader;
            bombsLabel.Visible  = isLeader;
            rounds.Visible = isLeader;
            limit.Visible = isLeader;

            if (isLeader && _lobby != null)
            {
                settingsTitle.Bounds = new Rectangle(620, 90, 320, 30);
                // Labels above textboxes for clarity
                roundsLabel.Bounds  = new Rectangle(620, 160, 260, 22);
                rounds.Bounds       = new Rectangle(620, 186, 260, 44);
                bombsLabel.Bounds   = new Rectangle(620, 238, 260, 22);
                limit.Bounds        = new Rectangle(620, 264, 260, 44);
                rounds.Update(t,m,k); limit.Update(t,m,k);
            }

            if (k.IsKeyDown(Keys.Escape)) _game.Screens.Show("menu");
        }
        public void Draw(SpriteBatch sb)
        {
            if (_lobby == null)
            {
                sb.DrawString(Ui.Font, "No lobby.", new Vector2(160, 140), Color.White);
                back.Draw(sb);
                return;
            }
            var header = "Lobby Code";
            sb.DrawString(Ui.Font, header, new Vector2(160, 90), new Color(200,220,255));
            var box = new Rectangle(160, 120, 320, 84);
            sb.DrawRect(box, new Color(25,25,25));
            sb.DrawFrame(box, new Color(70,70,70), 3);
            var measure = Ui.Font.MeasureString(_code);
            var scale = Math.Min( (box.Width-20) / Math.Max(1f, measure.X), (box.Height-20) / Math.Max(1f, measure.Y) );
            sb.DrawString(Ui.Font, _code, new Vector2(box.X + 10, box.Y + 10), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            sb.DrawString(Ui.Font, $"Status: {_lobby.Status}", new Vector2(160, 260), new Color(180,200,240));
            sb.DrawString(Ui.Font, "Players:", new Vector2(160, 300), Color.White);
            int y = 330;
            for (int i=0;i<_lobby.Players.Count;i++)
            {
                var u = _lobby.Players[i];
                sb.DrawString(Ui.Font, $"{i+1}. {u.Username}", new Vector2(180, y), Color.White);
                y += Ui.Font.LineSpacing + 6;
            }
            if (!string.IsNullOrEmpty(_info))
                sb.DrawString(Ui.Font, _info, new Vector2(160, y + 20), new Color(200,220,255));

            settingsTitle.Draw(sb);
            roundsLabel.Draw(sb);
            rounds.Draw(sb);
            bombsLabel.Draw(sb);
            limit.Draw(sb);
            start.Draw(sb);
            back.Draw(sb);
        }
        public void TextInput(char c) { rounds.TextInput(c); limit.TextInput(c); }
    }
}
