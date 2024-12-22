using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGame.ImGuiNet;
using System;
using System.Collections.Generic;

namespace MemHackGUI;
public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private static ImGuiRenderer GuiRenderer;
    bool _toolActive = true;

    List<(nint hWnd, string title, uint processId)> windows = [];
    int selectedWindowIndex = 0;

    int searchedValue = 0;

    int selectedPointerIndex = -1;
    List<IntPtr> foundPointers = [];

    int itemsPerPage = 20; // Number of items to display per page
    int currentPage = 0; // Track the current page (starting at 0)

    bool canClickNextScan = false;

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

        // set window title
        Window.Title = "MemHack";

        // Get all opened windows 
        windows = MemHack.Program.GetAllWindows();
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
            if (ImGui.BeginCombo("Select Window", windows[selectedWindowIndex].title))
            {
                for (int i = 0; i < windows.Count; i++)
                {
                    bool isSelected = (selectedWindowIndex == i);
                    if (ImGui.Selectable(windows[i].title, isSelected))
                        selectedWindowIndex = i;

                    // Highlight the selected item
                    if (isSelected)
                        ImGui.SetItemDefaultFocus();
                }

                ImGui.EndCombo();
            }

            ImGui.InputInt("Scan value", ref searchedValue);

            if (ImGui.Button("New Scan"))
            {
                selectedPointerIndex = -1;
                foundPointers = MemHack.Program.MemorySearch(windows[selectedWindowIndex].processId, searchedValue);
                // Code for handling the "New Scan" button click
            }

            ImGui.SameLine();
            if (!canClickNextScan)
                ImGui.BeginDisabled(); // Disable "Next Scan" button until the condition is met

            if (ImGui.Button("Next Scan"))
            {
                // Code for handling the "Next Scan" button click
            }

            if (!canClickNextScan)
                ImGui.EndDisabled(); // End the disabled block

            ImGui.Text($"Found Addresses: {foundPointers.Count}");

            // Pagination settings
            int totalItems = foundPointers.Count;
            int totalPages = (int)Math.Ceiling((float)totalItems / itemsPerPage);

            // Calculate the range of items to display based on the current page
            int startIndex = currentPage * itemsPerPage;
            int endIndex = Math.Min(startIndex + itemsPerPage, totalItems);

            // Begin scrollable area (child window)
            ImGui.BeginChild("Scrollable List", new System.Numerics.Vector2(0, 200), ImGuiChildFlags.None, ImGuiWindowFlags.AlwaysVerticalScrollbar);

            // Render the items for the current page
            for (int i = startIndex; i < endIndex; i++)
            {
                bool isSelected = (selectedPointerIndex == i);
                if (ImGui.Selectable($"0x{foundPointers[i]:X}", isSelected)) // Use item index or other string representation
                    selectedPointerIndex = i; // Update selected item index

                if (isSelected)
                    ImGui.SetItemDefaultFocus(); // Focus the selected item
            }

            // End child window
            ImGui.EndChild();

            // Pagination Controls
            ImGui.Spacing(); // Adds some space between the list and the pagination buttons

            // Previous Page Button
            if (currentPage > 0 && ImGui.Button("Previous Page"))
                currentPage--; // Go to the previous page

            if(currentPage > 0)
                ImGui.SameLine(); // Place the next button on the same line

            // Next Page Button
            if (currentPage < totalPages - 1 && ImGui.Button("Next Page"))
                currentPage++; // Go to the next page

            // Display Page Info (e.g., "Page 1 of 10")
            if(currentPage < totalPages)
                ImGui.SameLine();
            
            ImGui.Text($"Page {currentPage + 1} of {totalPages}");

            ImGui.End();
        }

        // End the main docking window
        ImGui.End();

        GuiRenderer.EndLayout();
    }
}
