using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;
using System.Diagnostics;

namespace App
{
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

    static class Program
    {
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
        private const float CharacterSpeed = 1.5f; // Will be replaced by tile-based movement
        private const float CharacterScale = 0.2f;

        private static Stopwatch _timer = new Stopwatch();
        private static double _timeSinceLastFrame = 0.0;
        private const double AnimationFps = 12.0;
        private const double TimePerFrame = 1.0 / AnimationFps;

        // Grid and Tile Settings
        private static int _tileVao;
        private static int _tileVbo;
        private static int _waterTextureId;
        private static int _grassTextureId;
        private static int _dirtTextureId;
        private static int _beachTextureId;
        private const int GridSize = 15;
        private const float TileScale = 0.2f;
        private const float IsometricYProjectionFactor = 0.8f; // User adjusted value for flatness

        private static string[,] TileLayout = new string[GridSize, GridSize];

        // Character grid position
        private static int _characterGridR; // Row
        private static int _characterGridC; // Column

        // Movement timing and input latching
        private static double _timeSinceLastMove = 0.0;
        private const double MoveCooldown = 1; // User set to 1 second
        private static int _intended_dr_grid = 0;
        private static int _intended_dc_grid = 0;
        private static int _intendedAnimationTarget = 0;
        private static bool _hasIntendedMove = false;

        static void Main()
        {
            GameWindowSettings gameWindowSettings = new GameWindowSettings();
            NativeWindowSettings nativeWindowSettings = new NativeWindowSettings();
            nativeWindowSettings.Size = new Vector2i(800, 600);
            nativeWindowSettings.Title = "M5 animacao sprite";
            nativeWindowSettings.Flags = ContextFlags.ForwardCompatible;

            GameWindow gameWindow = new GameWindow(gameWindowSettings, nativeWindowSettings);
            _gameWindowRef = gameWindow;

            gameWindow.Load += OnLoad;
            gameWindow.Unload += OnUnload;
            gameWindow.UpdateFrame += OnUpdateFrame;
            gameWindow.RenderFrame += OnRenderFrame;

            gameWindow.Run();
        }

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
            float[] vertices = {
                // Positions          Texture Coords
                -0.5f,  0.25f, 0.0f,  0.0f, 1.0f, // Top-Center
                 0.0f,  0.5f,  0.0f,  0.5f, 0.0f, // Right-Center
                 0.5f,  0.25f, 0.0f,  1.0f, 1.0f, // Bottom-Center
                 0.0f,  0.0f,  0.0f,  0.5f, 1.0f  // Left-Center
            };
            // Convert diamond to quad for texturing
            // For simplicity, we'll use a simple quad that fits the diamond for now
            // Adjust texture coordinates if needed for diamond shape
             float[] quadVertices = {
                // Positions          Texture Coords
                -0.5f,  0.5f, 0.0f,   0.0f, 1.0f, // Top-left
                -0.5f, -0.5f, 0.0f,   0.0f, 0.0f, // Bottom-left
                 0.5f, -0.5f, 0.0f,   1.0f, 0.0f, // Bottom-right

                -0.5f,  0.5f, 0.0f,   0.0f, 1.0f, // Top-left
                 0.5f, -0.5f, 0.0f,   1.0f, 0.0f, // Bottom-right
                 0.5f,  0.5f, 0.0f,   1.0f, 1.0f  // Top-right
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

            float s_coord = r + c; // Sum of grid coordinates for Y positioning
            float isoX_uncorrected = (c - r) * tileWidthScreen / 2.0f;
            float isoY_uncorrected = s_coord * effectiveTileHeightForPos / 2.0f;

            float finalX = isoX_uncorrected - gridCenterXOffset;
            float finalY = isoY_uncorrected - gridCenterYOffset;

            return new Vector2(finalX, finalY);
        }

        static void OnLoad()
        {
            GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            // Generate TileLayout
            for (int r = 0; r < GridSize; r++)
            {
                for (int c = 0; c < GridSize; c++)
                {
                    TileLayout[r, c] = "water"; // Default to water
                }
            }

            int center = GridSize / 2;
            int landRadius = GridSize / 3; // Approximate radius for the main landmass
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
                        // Prefer dirt if not already grass (for smoother transitions if radii overlap)
                        if (TileLayout[r,c] == "water") TileLayout[r, c] = "dirt";
                    }
                    else if (distFromCenter <= beachRadius)
                    {
                        // Prefer beach if still water
                        if (TileLayout[r,c] == "water") TileLayout[r, c] = "beach";
                    }
                }
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
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Error loading texture: {ex.Message} Path: {ex.FileName}");
                _gameWindowRef?.Close();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred during texture loading: {ex.ToString()}");
                _gameWindowRef?.Close();
                return;
            }

            // Initialize character grid position to the center
            _characterGridR = GridSize / 2;
            _characterGridC = GridSize / 2;
            characterPosition = GetWorldPositionForGridCoordinates(_characterGridR, _characterGridC);
            
            // Start character facing down (idle)
            currentAnimation = 3; 
            _intendedAnimationTarget = 3; 
            currentFrame = 0;

            _timer.Start();
        }

        static void OnUnload()
        {
            _timer.Stop();

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(_spriteVbo);
            GL.DeleteBuffer(_tileVbo); // Delete tile VBO

            GL.UseProgram(0);
            GL.DeleteProgram(_shaderProgram);

            GL.DeleteTexture(_spriteTextureId);
            GL.DeleteTexture(_waterTextureId);
            GL.DeleteTexture(_grassTextureId);
            GL.DeleteTexture(_dirtTextureId);
            GL.DeleteTexture(_beachTextureId);

            GL.DeleteVertexArray(_spriteVao);
            GL.DeleteVertexArray(_tileVao); // Delete tile VAO
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

            int frame_dr_grid = 0; 
            int frame_dc_grid = 0; 
            int frame_animationTarget = currentAnimation; 
            bool frame_inputProcessed = false;

            bool keyW = keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up);
            bool keyS = keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down);
            bool keyA = keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left);
            bool keyD = keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right);

            if (keyW && keyA) { frame_inputProcessed = true; frame_dr_grid = 1; frame_dc_grid = 0; frame_animationTarget = 0; }    // Visual UP-LEFT (NW) -> Grid S, Anim N
            else if (keyW && keyD) { frame_inputProcessed = true; frame_dr_grid = 0; frame_dc_grid = 1; frame_animationTarget = 0; } // Visual UP-RIGHT (NE) -> Grid E, Anim N
            else if (keyS && keyA) { frame_inputProcessed = true; frame_dr_grid = 0; frame_dc_grid = -1; frame_animationTarget = 3; } // Visual DOWN-LEFT (SW) -> Grid W, Anim S
            else if (keyS && keyD) { frame_inputProcessed = true; frame_dr_grid = -1; frame_dc_grid = 0; frame_animationTarget = 3; } // Visual DOWN-RIGHT (SE) -> Grid N, Anim S
            else if (keyW) { frame_inputProcessed = true; frame_dr_grid = 1; frame_dc_grid = 1; frame_animationTarget = 0; }      // Visual PURE UP -> Grid SE, Anim N
            else if (keyS) { frame_inputProcessed = true; frame_dr_grid = -1; frame_dc_grid = -1; frame_animationTarget = 3; }   // Visual PURE DOWN -> Grid NW, Anim S
            else if (keyA) { frame_inputProcessed = true; frame_dr_grid = 1; frame_dc_grid = -1; frame_animationTarget = 2; }    // Visual PURE LEFT -> Grid SW, Anim Right (flipped)
            else if (keyD) { frame_inputProcessed = true; frame_dr_grid = -1; frame_dc_grid = 1; frame_animationTarget = 1; }    // Visual PURE RIGHT -> Grid NE, Anim Left (flipped)
            
            if (frame_inputProcessed)
            {
                _intended_dr_grid = frame_dr_grid;
                _intended_dc_grid = frame_dc_grid;
                _intendedAnimationTarget = frame_animationTarget;
                _hasIntendedMove = true;
                // currentAnimation will be updated in the animation logic section
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
                        if (TileLayout[nextR, nextC] != "water")
                        {
                            _characterGridR = nextR;
                            _characterGridC = nextC;
                            characterPosition = GetWorldPositionForGridCoordinates(_characterGridR, _characterGridC);
                            characterMovedThisFrame = true; // Critical: set this flag
                            _timeSinceLastMove = 0.0; 
                        }
                    }
                }
            }

            _timeSinceLastFrame += args.Time;

            if (characterMovedThisFrame) { 
                currentFrame = 0; 
                currentAnimation = _intendedAnimationTarget; // Ensure animation matches move direction
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
            
            _gameWindowRef.Title = $"Grid:({_characterGridC},{_characterGridR}) Anim:{currentAnimation} Frame:{currentFrame} Pos:({characterPosition.X:F2},{characterPosition.Y:F2})";
        }

        static void OnRenderFrame(FrameEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(_shaderProgram);

            GL.BindVertexArray(_tileVao);
            int posLoc = GL.GetUniformLocation(_shaderProgram, "uPositionOffset");
            int scaleLoc = GL.GetUniformLocation(_shaderProgram, "uScale");
            int texOffsetLoc = GL.GetUniformLocation(_shaderProgram, "uTexOffset"); 

            GL.Uniform2(scaleLoc, new Vector2(TileScale, TileScale));
            GL.Uniform2(texOffsetLoc, Vector2.Zero); 

            float tileWidthScreen = TileScale; 
            float effectiveTileHeightForPos = TileScale * IsometricYProjectionFactor; // Use the class constant

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
                    }
                    GL.ActiveTexture(TextureUnit.Texture0);
                    GL.BindTexture(TextureTarget.Texture2D, currentTileTextureId);
                    GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
                }
            }

            GL.BindVertexArray(_spriteVao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _spriteTextureId);

            posLoc = GL.GetUniformLocation(_shaderProgram, "uPositionOffset"); 
            scaleLoc = GL.GetUniformLocation(_shaderProgram, "uScale");
            texOffsetLoc = GL.GetUniformLocation(_shaderProgram, "uTexOffset");

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
                Console.WriteLine($"Shader compilation error: {infoLog}");
            }
        }

        static void CheckProgramLinking(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
            {
                string infoLog = GL.GetProgramInfoLog(program);
                Console.WriteLine($"Program linking error: {infoLog}");
            }
        }
    }
}
