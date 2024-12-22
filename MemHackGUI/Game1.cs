using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.ImGuiNet;

namespace MemHackGUI;
public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private static ImGuiRenderer GuiRenderer;
    bool _toolActive = true;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // set window size
        _graphics.PreferredBackBufferWidth = 500;
        _graphics.PreferredBackBufferHeight = 600;
        _graphics.ApplyChanges();

        // allow window resize
        Window.AllowUserResizing = true;
    }

    protected override void Initialize()
    {
        GuiRenderer = new ImGuiRenderer(this);
        GuiRenderer.RebuildFontAtlas();

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        base.Draw(gameTime);

        GuiRenderer.BeginLayout(gameTime);

        // Get the application window size
        var viewport = GraphicsDevice.Viewport;
        var windowSize = new System.Numerics.Vector2(viewport.Width, viewport.Height);

        // Get ImGui IO for configuration
        var io = ImGui.GetIO();

        // Enable docking
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;

        // Create a root dockspace that occupies the entire game window
        ImGui.SetNextWindowPos(System.Numerics.Vector2.Zero);          // Position at the top-left corner
        ImGui.SetNextWindowSize(windowSize);           // Match the size of the game window
        ImGui.SetNextWindowViewport(ImGui.GetMainViewport().ID);

        // Set window flags to disable resizing, moving, and closing
        var windowFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove;
        windowFlags |= ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoDocking;

        // Begin the main docking window
        ImGui.Begin("DockSpace Window", windowFlags);

        // Create the dockspace
        var dockspaceId = ImGui.GetID("MyDockspace");
        ImGui.DockSpace(dockspaceId, System.Numerics.Vector2.Zero, ImGuiDockNodeFlags.AutoHideTabBar);

        // Ensure dockable windows are placed inside the dockspace
        ImGui.SetNextWindowDockID(dockspaceId, ImGuiCond.Always);

        // Add content to the docked window
        var dockedWindowFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoMove;
        if (ImGui.Begin("MemHack", dockedWindowFlags))
        {
            ImGui.Text("This panel is docked inside the game window.");
            ImGui.End();
        }

        // End the main docking window
        ImGui.End();

        GuiRenderer.EndLayout();
    }
}
