using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Bomberman.Client.Net;
using Bomberman.Client.UI;
using Bomberman.Client.Screens;
using System;

namespace Bomberman.Client;

public class Game1 : Game
{
    GraphicsDeviceManager _g;
    SpriteBatch _sb = null!;
    public ApiClient Api = new("http://localhost:5202"); // REST base
    public string WsUrl = "ws://localhost:5202/api/game/state";
    public GameSocket? Socket;
    public ScreenManager Screens = new();
    public LobbyScreen LobbyScreenRef = null!;

    public Game1()
    {
        _g = new GraphicsDeviceManager(this);
        _g.PreferredBackBufferWidth = 960;
        _g.PreferredBackBufferHeight = 540;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        Window.TextInput += (_, e) => Screens.TextInput(e.Character);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _sb = new SpriteBatch(GraphicsDevice);
        Ui.Pixel = new Texture2D(GraphicsDevice, 1, 1);
        Ui.Pixel.SetData(new[] { Color.White });
        Ui.Font = Content.Load<SpriteFont>("Default");

        Screens.Add("login", new LoginScreen(this));
        Screens.Add("menu", new MenuScreen(this));
        Screens.Add("leaderboard", new LeaderboardScreen(this));
        LobbyScreenRef = new LobbyScreen(this);
        Screens.Add("lobby", LobbyScreenRef);
        Screens.Add("ws", new WsScreen(this));
        Screens.Show("login");
    }

    protected override void Update(GameTime gameTime)
    {
        if (Keyboard.GetState().IsKeyDown(Keys.F11))
        {
            _g.IsFullScreen = !_g.IsFullScreen; _g.ApplyChanges();
        }
        Screens.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(70,100,170));
        _sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null);
        Screens.Draw(_sb);
        _sb.End();
        base.Draw(gameTime);
    }
}
