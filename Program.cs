using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;
using System.Diagnostics;

namespace App
{


    static class Program
    {
        #region variables
        private static int _spriteVao;
        private static int _spriteVbo;
        private static int _shaderProgram;
        private static int _spriteTextureId;
        private static GameWindow _gameWindowRef;

        private const string SpriteSheetPath = "Textures/Unarmed_Walk_full.png";
        private const int nAnimations = 4;
        private const int nFrames = 6;
        private static float ds = 1.0f / (float)nFrames;
        private static float dt = 1.0f / (float)nAnimations;

        private static int currentAnimation = 0;
        private static int currentFrame = 0;
        private static float offsetS = 0.0f;
        private static float offsetT = 0.0f;

        private static Vector2 characterPosition = Vector2.Zero;
        private const float CharacterScale = 0.2f;

        private static Stopwatch _timer = new Stopwatch();
        private static double _timeSinceLastFrame = 0.0;
        private const double AnimationFps = 12.0;
        private const double TimePerFrame = 1.0 / AnimationFps;

        private static int _tileVao;
        private static int _tileVbo;
        private static int _waterTextureId;
        private static int _grassTextureId;
        private static int _dirtTextureId;
        private static int _beachTextureId;
        private static int _roadEWTextureId;
        private static int _roadNSTextureId;
        private static int _endETextureId;
        private static int _endNTextureId;
        private static int _endSTextureId;
        private static int _endWTextureId;
        private static int _treeShortTextureId;
        private static int _treeTallTextureId;
        private static int _keyTextureId;
        private static int _chestClosedTextureId;
        private static int _chestOpenTextureId;
        private const int GridSize = 15;
        private const float TileScale = 0.2f;
        private const float IsometricYProjectionFactor = 0.8f;

        private static string[,] TileLayout = new string[GridSize, GridSize];

        private static int _characterGridR;
        private static int _characterGridC;

        private static double _timeSinceLastMove = 0.0;
        private const double MoveCooldown = 1;
        private static int _intended_dr_grid = 0;
        private static int _intended_dc_grid = 0;
        private static int _intendedAnimationTarget = 0;
        private static bool _hasIntendedMove = false;

        public class Tree
        {
            public int R { get; }
            public int C { get; }
            public string Type { get; }
            public int TextureId { get; }

            public Tree(int r, int c, string type, int textureId)
            {
                R = r;
                C = c;
                Type = type;
                TextureId = textureId;
            }
        }
        private static List<Tree> _trees = new List<Tree>();
        private const float TreeShortScale = 0.15f;
        private const float TreeTallScale = 0.2f;

        private struct TreeDefinition { public int R, C; public string Type; public TreeDefinition(int r, int c, string type) { R=r; C=c; Type=type; }}
        private static List<TreeDefinition> _treeDefinitions = new List<TreeDefinition>
        {
            new TreeDefinition(5, 4, "tall"), new TreeDefinition(5, 5, "short"), new TreeDefinition(5, 6, "tall"),
            new TreeDefinition(6, 3, "short"),
            new TreeDefinition(4, 5, "tall"),

            new TreeDefinition(4, 9, "tall"), new TreeDefinition(5, 9, "short"), new TreeDefinition(6, 9, "tall"),
            new TreeDefinition(5, 11, "short"),
            new TreeDefinition(3, 10, "tall"),

            new TreeDefinition(9, 3, "tall"), new TreeDefinition(9, 4, "short"), new TreeDefinition(9, 5, "tall"),
            new TreeDefinition(10, 2, "short"),
            new TreeDefinition(8, 2, "tall"),

            new TreeDefinition(8, 10, "tall"), new TreeDefinition(9, 10, "short"), new TreeDefinition(10, 10, "tall"),
            new TreeDefinition(9, 12, "short"),
            new TreeDefinition(11, 9, "tall")
        };

        private static int _keyGridR;
        private static int _keyGridC;
        private static bool _keyCollected = false;
        private const float KeyScale = 0.1f;

        private static int _chestGridR;
        private static int _chestGridC;
        private static bool _chestIsOpen = false;
        private const float ChestScale = 0.18f;
        private static bool _playerHasKey = false;
        private static bool _gameOver = false;
        #endregion

        static void Main()
        {
            GameWindowSettings gameWindowSettings = new GameWindowSettings();
            NativeWindowSettings nativeWindowSettings = new NativeWindowSettings();
            nativeWindowSettings.ClientSize = new Vector2i(800, 600);
            nativeWindowSettings.Flags = ContextFlags.ForwardCompatible;

            GameWindow gameWindow = new GameWindow(gameWindowSettings, nativeWindowSettings);
            _gameWindowRef = gameWindow;

            gameWindow.Load += OnLoad;
            gameWindow.Unload += OnUnload;
            gameWindow.UpdateFrame += OnUpdateFrame;
            gameWindow.RenderFrame += OnRenderFrame;

            gameWindow.Run();
        }

        #region gridtiles
        private static int CreateSpriteQuad()
        {
            float[] vertices = {
                -0.5f,  0.5f, 0.0f, 0.0f, dt,
                -0.5f, -0.5f, 0.0f, 0.0f, 0.0f,
                 0.5f, -0.5f, 0.0f, ds,   0.0f,

                -0.5f,  0.5f, 0.0f, 0.0f, dt,
                 0.5f, -0.5f, 0.0f, ds,   0.0f,
                 0.5f,  0.5f, 0.0f, ds,   dt
            };

            int vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            _spriteVbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _spriteVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            int stride = 5 * sizeof(float);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);

            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            return vao;
        }

        private static int CreateTileQuad()
        {
            float[] quadVertices = {
                -0.5f,  0.5f, 0.0f,   0.0f, 1.0f,
                -0.5f, -0.5f, 0.0f,   0.0f, 0.0f,
                 0.5f, -0.5f, 0.0f,   1.0f, 0.0f,

                -0.5f,  0.5f, 0.0f,   0.0f, 1.0f,
                 0.5f, -0.5f, 0.0f,   1.0f, 0.0f,
                 0.5f,  0.5f, 0.0f,   1.0f, 1.0f
            };

            int vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            _tileVbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _tileVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quadVertices.Length * sizeof(float), quadVertices, BufferUsageHint.StaticDraw);

            int stride = 5 * sizeof(float);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.EnableVertexAttribArray(1);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindVertexArray(0);
            return vao;
        }

        private static Vector2 GetWorldPositionForGridCoordinates(int r, int c)
        {
            float tileWidthScreen = TileScale;
            float effectiveTileHeightForPos = TileScale * IsometricYProjectionFactor;

            int centerRow = GridSize / 2;
            int centerCol = GridSize / 2;
            float gridCenterXOffset = (centerCol - centerRow) * tileWidthScreen / 2.0f;
            float gridCenterYOffset = (centerCol + centerRow) * effectiveTileHeightForPos / 2.0f;

            float s_coord = r + c;
            float isoX_uncorrected = (c - r) * tileWidthScreen / 2.0f;
            float isoY_uncorrected = s_coord * effectiveTileHeightForPos / 2.0f;

            float finalX = isoX_uncorrected - gridCenterXOffset;
            float finalY = isoY_uncorrected - gridCenterYOffset;

            return new Vector2(finalX, finalY);
        }
        #endregion

        #region shaders
        static void OnLoad()
        {
            GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    TileLayout[r, c] = "water";
                }
            }

            int center = GridSize / 2;
            int landRadius = GridSize / 3; 
            int dirtRadius = landRadius + 1;
            int beachRadius = landRadius + 2;

            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    double distFromCenter = Math.Sqrt(Math.Pow(r - center, 2) + Math.Pow(c - center, 2));

                    if (distFromCenter <= landRadius)
                    {
                        TileLayout[r, c] = "grass";
                    }
                    else if (distFromCenter <= dirtRadius)
                    {
                        if (TileLayout[r,c] == "water") TileLayout[r, c] = "dirt";
                    }
                    else if (distFromCenter <= beachRadius)
                    {
                        if (TileLayout[r,c] == "water") TileLayout[r, c] = "beach";
                    }
                }
            }

            int roadRow = GridSize / 2;
            int min_land_col_on_row = -1;
            int max_land_col_on_row = -1;

            for (int c = 0; c < GridSize; c++)
            {
                if (TileLayout[roadRow, c] == "grass" || TileLayout[roadRow, c] == "dirt")
                {
                    if (min_land_col_on_row == -1)
                    {
                        min_land_col_on_row = c;
                    }
                    max_land_col_on_row = c;
                }
            }

            int road_actual_start_col = -1;
            int road_actual_end_col = -1;

            if (min_land_col_on_row != -1 && max_land_col_on_row >= min_land_col_on_row)
            {
                road_actual_start_col = min_land_col_on_row + 1;
                road_actual_end_col = max_land_col_on_row - 4; 

                if (road_actual_start_col <= road_actual_end_col) 
                {
                    if (road_actual_start_col == road_actual_end_col) 
                    {
                        TileLayout[roadRow, road_actual_start_col] = "roadEW";
                    }
                    else 
                    {
                        TileLayout[roadRow, road_actual_start_col] = "endE"; 
                        TileLayout[roadRow, road_actual_end_col] = "endW";   

                        for (int c_road = road_actual_start_col + 1; c_road < road_actual_end_col; c_road++)
                        {
                            TileLayout[roadRow, c_road] = "roadEW"; 
                        }
                    }
                }
                else
                {
                    road_actual_start_col = -1;
                    road_actual_end_col = -1;
                }
            }

            List<Tuple<int, int>> riverWaypoints = new List<Tuple<int, int>>();
            if (road_actual_start_col != -1 && road_actual_end_col != -1 && road_actual_start_col <= road_actual_end_col)
            {
                int roadCrossingPointR = roadRow;
                int roadCrossingPointC = road_actual_start_col + ((road_actual_end_col - road_actual_start_col + 1) / 2);

                riverWaypoints.Add(Tuple.Create(0, roadCrossingPointC - 3));
                riverWaypoints.Add(Tuple.Create(2, roadCrossingPointC - 2));
                riverWaypoints.Add(Tuple.Create(4, roadCrossingPointC - 2));
                riverWaypoints.Add(Tuple.Create(roadCrossingPointR - 1, roadCrossingPointC -1));
                riverWaypoints.Add(Tuple.Create(roadCrossingPointR, roadCrossingPointC));
                riverWaypoints.Add(Tuple.Create(roadCrossingPointR + 1, roadCrossingPointC + 1));
                riverWaypoints.Add(Tuple.Create(roadCrossingPointR + 3, roadCrossingPointC + 1));
                riverWaypoints.Add(Tuple.Create(GridSize - 3, roadCrossingPointC + 2));
                riverWaypoints.Add(Tuple.Create(GridSize - 1, roadCrossingPointC + 3));
            }
            else
            {
                int centerC = GridSize / 2;
                riverWaypoints.Add(Tuple.Create(0, centerC - 2));
                riverWaypoints.Add(Tuple.Create(3, centerC - 1));
                riverWaypoints.Add(Tuple.Create(6, centerC -1));
                riverWaypoints.Add(Tuple.Create(GridSize / 2, centerC));
                riverWaypoints.Add(Tuple.Create(GridSize / 2 + 2, centerC + 1));
                riverWaypoints.Add(Tuple.Create(GridSize - 3, centerC + 2));
                riverWaypoints.Add(Tuple.Create(GridSize - 1, centerC + 2));
            }
            CarveRiverPath(riverWaypoints);

            if (GridSize > 10 && GridSize > 7)
            {
                TileLayout[10, 7] = "grass"; 
            }

            _spriteVao = CreateSpriteQuad();
            _tileVao = CreateTileQuad(); 
            SetupShaders();

            try
            {
                _spriteTextureId = TextureLoader.LoadTexture(SpriteSheetPath);
                _waterTextureId = TextureLoader.LoadTexture("tileset/water.png");
                _grassTextureId = TextureLoader.LoadTexture("tileset/grass.png");
                _dirtTextureId = TextureLoader.LoadTexture("tileset/dirt.png");
                _beachTextureId = TextureLoader.LoadTexture("tileset/beach.png");
                _roadEWTextureId = TextureLoader.LoadTexture("tileset/roadEW.png");
                _roadNSTextureId = TextureLoader.LoadTexture("tileset/roadNS.png");
                _endETextureId = TextureLoader.LoadTexture("tileset/endE.png");
                _endNTextureId = TextureLoader.LoadTexture("tileset/endN.png");
                _endSTextureId = TextureLoader.LoadTexture("tileset/endS.png");
                _endWTextureId = TextureLoader.LoadTexture("tileset/endW.png");
                _treeShortTextureId = TextureLoader.LoadTexture("tileset/treeShort.png");
                _treeTallTextureId = TextureLoader.LoadTexture("tileset/treeTall.png");
                _keyTextureId = TextureLoader.LoadTexture("tileset/key.png");
                _chestClosedTextureId = TextureLoader.LoadTexture("tileset/chestclosed.png");
                _chestOpenTextureId = TextureLoader.LoadTexture("tileset/chestopen.png");
            }
            catch (FileNotFoundException ex)
            {
                _gameWindowRef?.Close();
                return;
            }
            catch (Exception ex)
            {
                _gameWindowRef?.Close();
                return;
            }

            foreach (var def in _treeDefinitions)
            {
                if (def.R >= 0 && def.R < GridSize && def.C >= 0 && def.C < GridSize)
                {
                    string tileType = TileLayout[def.R, def.C];
                    if (tileType == "grass" || tileType == "dirt")
                    {
                        int texID = (def.Type == "short") ? _treeShortTextureId : _treeTallTextureId;
                        _trees.Add(new Tree(def.R, def.C, def.Type, texID));
                    }
                }
            }

            _characterGridR = GridSize / 2;
            _characterGridC = GridSize / 2;
            characterPosition = GetWorldPositionForGridCoordinates(_characterGridR, _characterGridC);
            
            currentAnimation = 3; 
            _intendedAnimationTarget = 3; 
            currentFrame = 0;

            _keyGridR = 11;
            _keyGridC = 4;
            _keyCollected = false;

            _chestGridR = 4;
            _chestGridC = 10;
            _chestIsOpen = false;
            
            _playerHasKey = false;

            _timer.Start();
        }

        static void OnUnload()
        {
            _timer.Stop();

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(_spriteVbo);
            GL.DeleteBuffer(_tileVbo);

            GL.UseProgram(0);
            GL.DeleteProgram(_shaderProgram);

            GL.DeleteTexture(_spriteTextureId);
            GL.DeleteTexture(_waterTextureId);
            GL.DeleteTexture(_grassTextureId);
            GL.DeleteTexture(_dirtTextureId);
            GL.DeleteTexture(_beachTextureId);
            GL.DeleteTexture(_roadEWTextureId);
            GL.DeleteTexture(_roadNSTextureId);
            GL.DeleteTexture(_endETextureId);
            GL.DeleteTexture(_endNTextureId);
            GL.DeleteTexture(_endSTextureId);
            GL.DeleteTexture(_endWTextureId);
            GL.DeleteTexture(_treeShortTextureId);
            GL.DeleteTexture(_treeTallTextureId);
            GL.DeleteTexture(_keyTextureId);
            GL.DeleteTexture(_chestClosedTextureId);
            GL.DeleteTexture(_chestOpenTextureId);

            GL.DeleteVertexArray(_spriteVao);
            GL.DeleteVertexArray(_tileVao);
        }

        static void SetupShaders()
        {
            string vertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec2 aTexCoord;

            uniform vec2 uPositionOffset;
            uniform vec2 uScale;
            uniform vec2 uTexOffset;

            out vec2 TexCoord;

            void main()
            {
                vec3 scaledPosition = aPosition;
                scaledPosition.xy *= uScale;
                scaledPosition.xy += uPositionOffset;
                gl_Position = vec4(scaledPosition, 1.0);
                TexCoord = aTexCoord + uTexOffset;
            }";

            string fragmentShaderSource = @"
            #version 330 core
            out vec4 FragColor;

            in vec2 TexCoord;

            uniform sampler2D textureSampler;

            void main()
            {
                FragColor = texture(textureSampler, TexCoord);
                if(FragColor.a < 0.1) discard;
            }";

            int vertexShader = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertexShader, vertexShaderSource); GL.CompileShader(vertexShader); CheckShaderCompilation(vertexShader);
            int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragmentShader, fragmentShaderSource); GL.CompileShader(fragmentShader); CheckShaderCompilation(fragmentShader);

            _shaderProgram = GL.CreateProgram();
            GL.AttachShader(_shaderProgram, vertexShader); GL.AttachShader(_shaderProgram, fragmentShader); GL.LinkProgram(_shaderProgram); CheckProgramLinking(_shaderProgram);

            GL.DetachShader(_shaderProgram, vertexShader);
            GL.DetachShader(_shaderProgram, fragmentShader);
            GL.DeleteShader(vertexShader); GL.DeleteShader(fragmentShader);

            GL.UseProgram(_shaderProgram);
            int samplerLoc = GL.GetUniformLocation(_shaderProgram, "textureSampler");
            GL.Uniform1(samplerLoc, 0);
            GL.UseProgram(0);
        }

        static void OnUpdateFrame(FrameEventArgs args)
        {
            var keyboard = _gameWindowRef.KeyboardState;
            _timeSinceLastMove += args.Time;

            if (keyboard.IsKeyDown(Keys.Escape))
            {
                _gameWindowRef.Close();
                return;
            }

            if (_gameOver) 
            {
                return; 
            }

            int frame_dr_grid = 0; 
            int frame_dc_grid = 0; 
            int frame_animationTarget = currentAnimation; 
            bool frame_inputProcessed = false;

            bool keyW = keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up);
            bool keyS = keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down);
            bool keyA = keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left);
            bool keyD = keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right);

            if (keyW && keyA) { frame_inputProcessed = true; frame_dr_grid = 1; frame_dc_grid = 0; frame_animationTarget = 0; }
            else if (keyW && keyD) { frame_inputProcessed = true; frame_dr_grid = 0; frame_dc_grid = 1; frame_animationTarget = 0; }
            else if (keyS && keyA) { frame_inputProcessed = true; frame_dr_grid = 0; frame_dc_grid = -1; frame_animationTarget = 3; }
            else if (keyS && keyD) { frame_inputProcessed = true; frame_dr_grid = -1; frame_dc_grid = 0; frame_animationTarget = 3; }
            else if (keyW) { frame_inputProcessed = true; frame_dr_grid = 1; frame_dc_grid = 1; frame_animationTarget = 0; }
            else if (keyS) { frame_inputProcessed = true; frame_dr_grid = -1; frame_dc_grid = -1; frame_animationTarget = 3; }
            else if (keyA) { frame_inputProcessed = true; frame_dr_grid = 1; frame_dc_grid = -1; frame_animationTarget = 2; }
            else if (keyD) { frame_inputProcessed = true; frame_dr_grid = -1; frame_dc_grid = 1; frame_animationTarget = 1; }
            
            if (frame_inputProcessed)
            {
                _intended_dr_grid = frame_dr_grid;
                _intended_dc_grid = frame_dc_grid;
                _intendedAnimationTarget = frame_animationTarget;
                _hasIntendedMove = true;
            }
            else
            {
                _hasIntendedMove = false; 
            }
            
            bool characterMovedThisFrame = false;

            if (_timeSinceLastMove >= MoveCooldown)
            {
                if (_hasIntendedMove) 
                {
                    int nextR = _characterGridR + _intended_dr_grid;
                    int nextC = _characterGridC + _intended_dc_grid;

                    if (nextR >= 0 && nextR < GridSize && nextC >= 0 && nextC < GridSize)
                    {
                        string targetTileType = TileLayout[nextR, nextC];
                        bool isTreeBlocking = _trees.Any(tree => tree.R == nextR && tree.C == nextC);

                        if (targetTileType != "water" && !isTreeBlocking) 
                        {
                            _characterGridR = nextR;
                            _characterGridC = nextC;
                            characterPosition = GetWorldPositionForGridCoordinates(_characterGridR, _characterGridC);
                            characterMovedThisFrame = true; 
                            _timeSinceLastMove = 0.0; 

                            if (!_keyCollected && _characterGridR == _keyGridR && _characterGridC == _keyGridC)
                            {
                                _keyCollected = true;
                                _playerHasKey = true;
                            }

                            if (_characterGridR == _chestGridR && _characterGridC == _chestGridC)
                            {
                                if (_playerHasKey && !_chestIsOpen)
                                {
                                    _chestIsOpen = true;
                                    _gameWindowRef.Title = "Voce abriu o bau! game over.";
                                    _gameOver = true;
                                }
                            }
                        }
                    }
                }
            }

            _timeSinceLastFrame += args.Time;

            if (characterMovedThisFrame) { 
                currentFrame = 0; 
                currentAnimation = _intendedAnimationTarget; 
            }

            if (_hasIntendedMove) { 
                currentAnimation = _intendedAnimationTarget; 

                if (_timeSinceLastMove < MoveCooldown) { 
                    if (_timeSinceLastFrame >= TimePerFrame) {
                        currentFrame = (currentFrame + 1) % nFrames;
                        _timeSinceLastFrame -= TimePerFrame;
                    }
                } else {
                    currentFrame = 0; 
                }
            } else { 
                currentAnimation = 3; 
                currentFrame = 0;     
            }

            offsetS = (float)currentFrame * ds;
            offsetT = (float)currentAnimation * dt; 
            
            if (!_gameOver)
            {
                _gameWindowRef.Title = $"Grid:({_characterGridC},{_characterGridR}) Anim:{currentAnimation} Frame:{currentFrame} Pos:({characterPosition.X:F2},{characterPosition.Y:F2})";
            }
        }

        static void OnRenderFrame(FrameEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(_shaderProgram);

            int posLoc = GL.GetUniformLocation(_shaderProgram, "uPositionOffset");
            int scaleLoc = GL.GetUniformLocation(_shaderProgram, "uScale");
            int texOffsetLoc = GL.GetUniformLocation(_shaderProgram, "uTexOffset"); 

            GL.BindVertexArray(_tileVao);
            GL.Uniform2(scaleLoc, new Vector2(TileScale, TileScale));
            GL.Uniform2(texOffsetLoc, Vector2.Zero); 

            float tileWidthScreen = TileScale; 
            float effectiveTileHeightForPos = TileScale * IsometricYProjectionFactor; 

            int centerRow = GridSize / 2;
            int centerCol = GridSize / 2;
            float gridCenterXOffset = (centerCol - centerRow) * tileWidthScreen / 2.0f;
            float gridCenterYOffset = (centerCol + centerRow) * effectiveTileHeightForPos / 2.0f;

            for (int s = 2 * (GridSize - 1); s >= 0; s--) 
            {
                int r_min = Math.Max(0, s - (GridSize - 1));
                int r_max = Math.Min(GridSize - 1, s);

                for (int r_loop = r_min; r_loop <= r_max; r_loop++) 
                {
                    int c_loop = s - r_loop; 

                    float isoX_uncorrected = (c_loop - r_loop) * tileWidthScreen / 2.0f;
                    float isoY_uncorrected = s * effectiveTileHeightForPos / 2.0f; 

                    float finalX = isoX_uncorrected - gridCenterXOffset;
                    float finalY = isoY_uncorrected - gridCenterYOffset;

                    GL.Uniform2(posLoc, new Vector2(finalX, finalY));

                    int currentTileTextureId = _dirtTextureId; 
                    switch (TileLayout[r_loop, c_loop]) 
                    {
                        case "water": currentTileTextureId = _waterTextureId; break;
                        case "grass": currentTileTextureId = _grassTextureId; break;
                        case "beach": currentTileTextureId = _beachTextureId; break;
                        case "dirt": currentTileTextureId = _dirtTextureId; break;
                        case "roadEW": currentTileTextureId = _roadEWTextureId; break;
                        case "roadNS": currentTileTextureId = _roadNSTextureId; break;
                        case "endE": currentTileTextureId = _endETextureId; break;
                        case "endN": currentTileTextureId = _endNTextureId; break;
                        case "endS": currentTileTextureId = _endSTextureId; break;
                        case "endW": currentTileTextureId = _endWTextureId; break;
                        default: currentTileTextureId = _dirtTextureId; break; 
                    }
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, currentTileTextureId);
                    GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
                }
            }

            var sortedTrees = _trees.OrderBy(t => t.R + t.C).ToList();
            GL.BindVertexArray(_tileVao);
            GL.Uniform2(texOffsetLoc, Vector2.Zero);

            foreach (var tree in sortedTrees)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, tree.TextureId);

                Vector2 tileCenterPos = GetWorldPositionForGridCoordinates(tree.R, tree.C);
                float treeScaleValue = (tree.Type == "short") ? TreeShortScale : TreeTallScale;
                
                Vector2 treeRenderPos = new Vector2(tileCenterPos.X, tileCenterPos.Y + 0.5f * treeScaleValue);
                
                GL.Uniform2(posLoc, treeRenderPos);
                GL.Uniform2(scaleLoc, new Vector2(treeScaleValue * 0.25f, treeScaleValue)); 

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            }

            if (!_keyCollected)
            {
                GL.BindVertexArray(_tileVao);
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _keyTextureId);

                Vector2 keyTileCenterPos = GetWorldPositionForGridCoordinates(_keyGridR, _keyGridC);
                Vector2 keyRenderPos = new Vector2(keyTileCenterPos.X, keyTileCenterPos.Y + 0.5f * KeyScale * 0.5f);

                GL.Uniform2(posLoc, keyRenderPos);
                GL.Uniform2(scaleLoc, new Vector2(KeyScale, KeyScale));
                GL.Uniform2(texOffsetLoc, Vector2.Zero);
                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            }

            GL.BindVertexArray(_tileVao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _chestIsOpen ? _chestOpenTextureId : _chestClosedTextureId);
            
            Vector2 chestTileCenterPos = GetWorldPositionForGridCoordinates(_chestGridR, _chestGridC);
            Vector2 chestRenderPos = new Vector2(chestTileCenterPos.X, chestTileCenterPos.Y + 0.5f * ChestScale * 0.5f);

            GL.Uniform2(posLoc, chestRenderPos);
            GL.Uniform2(scaleLoc, new Vector2(ChestScale, ChestScale));
            GL.Uniform2(texOffsetLoc, Vector2.Zero);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            GL.BindVertexArray(_spriteVao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _spriteTextureId);
            
            GL.Uniform2(posLoc, characterPosition); 
            GL.Uniform2(scaleLoc, new Vector2(CharacterScale, CharacterScale));
            GL.Uniform2(texOffsetLoc, new Vector2(offsetS, offsetT));

            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.BindVertexArray(0);
            GL.UseProgram(0);

            _gameWindowRef.SwapBuffers();
        }

        static void CheckShaderCompilation(int shader)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetShaderInfoLog(shader);
            }
        }

        static void CheckProgramLinking(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
            }
        }
        #endregion

        #region drawriver
        private static void SetTileToWater(int r, int c)
        {
            if (r >= 0 && r < GridSize && c >= 0 && c < GridSize)
            {
                TileLayout[r, c] = "water";
            }
        }

        private static void DrawRiverSegmentBetween(int r1, int c1, int r2, int c2)
        {
            int curR = r1;
            int curC = c1;

            SetTileToWater(curR, curC);

            while (curR != r2 || curC != c2)
            {
                int dr = Math.Sign(r2 - curR);
                int dc = Math.Sign(c2 - curC);

                if (curR != r2)
                {
                    curR += dr;
                }
                else if (curC != c2)
                {
                    curC += dc;
                }
                SetTileToWater(curR, curC);
            }
        }

        private static void CarveRiverPath(List<Tuple<int, int>> waypoints)
        {
            if (waypoints == null || waypoints.Count < 2) return;

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                Tuple<int, int> start = waypoints[i];
                Tuple<int, int> end = waypoints[i + 1];
                DrawRiverSegmentBetween(start.Item1, start.Item2, end.Item1, end.Item2);
            }
        }
        #endregion
    }

    static class TextureLoader
    {
        public static int LoadTexture(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("programa sem acesso ao spritesheet", path);
            }

            int handle = GL.GenTexture();
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, handle);

            StbImage.stbi_set_flip_vertically_on_load(1);
            using (Stream stream = File.OpenRead(path))
            {
                ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, image.Width, image.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, image.Data);
            }

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GL.BindTexture(TextureTarget.Texture2D, 0);

            return handle;
        }
    }
}
