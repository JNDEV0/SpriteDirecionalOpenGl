using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;
using System.Diagnostics;
using System.Collections.Generic;

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
        private const float CharacterSpeed = 1.5f;
        private const float CharacterScale = 0.6f;

        private static Stopwatch _timer = new Stopwatch();
        private static double _timeSinceLastFrame = 0.0;
        private const double AnimationFps = 12.0;
        private const double TimePerFrame = 1.0 / AnimationFps;

        // Parallax Layers
        private static List<int> _layerTextureIds = new List<int>();
        private static string[] _layerPaths = new string[]
        {
            "Layers/layer01_Ground.png",
            "Layers/layer02_Trees.png",
            "Layers/layer03_Hills_1.png",
            "Layers/layer04_Hills_2.png",
            "Layers/layer05_Clouds.png",
            "Layers/layer06_Rocks.png",
            "Layers/layer07_Sky.png"
        };
        // Parallax factors - closer layers have higher values (move more)
        private static float[] _parallaxFactors = new float[]
        {
            0.8f, // Ground
            0.6f, // Trees
            0.4f, // Hills_1
            0.3f, // Hills_2
            0.2f, // Clouds
            0.1f, // Rocks
            0.05f // Sky
        };
        private static int _backgroundVao;
        private static int _backgroundVbo;

        static void Main()
        {
            GameWindowSettings gameWindowSettings = new GameWindowSettings();
            NativeWindowSettings nativeWindowSettings = new NativeWindowSettings();
            nativeWindowSettings.Size = new Vector2i(800, 600);
            nativeWindowSettings.Title = "M5 animacao sprite com parallax";
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

        private static int CreateBackgroundQuad()
        {
            float[] vertices = {
                // Positions    // Texture Coords
                -1.0f,  1.0f, 0.0f, 0.0f, 1.0f, // Top-left
                -1.0f, -1.0f, 0.0f, 0.0f, 0.0f, // Bottom-left
                 1.0f, -1.0f, 0.0f, 1.0f, 0.0f, // Bottom-right

                -1.0f,  1.0f, 0.0f, 0.0f, 1.0f, // Top-left
                 1.0f, -1.0f, 0.0f, 1.0f, 0.0f, // Bottom-right
                 1.0f,  1.0f, 0.0f, 1.0f, 1.0f  // Top-right
            };

            int vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);

            _backgroundVbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _backgroundVbo);
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

        static void OnLoad()
        {
            GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _spriteVao = CreateSpriteQuad();
            _backgroundVao = CreateBackgroundQuad(); // Create VAO for background layers
            SetupShaders();

            try
            {
                _spriteTextureId = TextureLoader.LoadTexture(SpriteSheetPath);
                // Load layer textures
                foreach (string path in _layerPaths)
                {
                    _layerTextureIds.Add(TextureLoader.LoadTexture(path));
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Error loading texture: {ex.Message} ({ex.FileName})");
                _gameWindowRef?.Close();
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred during loading: {ex.Message}");
                _gameWindowRef?.Close();
                return;
            }
            
            // Set initial character position (on the ground)
            // Assuming ground is roughly at the bottom of the screen, adjust Y as needed.
            // Character's origin is center, quad is -0.5 to 0.5. So -1 + scale*0.5 is bottom.
            characterPosition = new Vector2(0.0f, -1.0f + CharacterScale * 0.8f);

            _timer.Start();
        }

        static void OnUnload()
        {
            _timer.Stop();

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(_spriteVbo);
            GL.DeleteBuffer(_backgroundVbo); // Delete background VBO

            GL.UseProgram(0);
            GL.DeleteProgram(_shaderProgram);

            GL.DeleteTexture(_spriteTextureId);
            // Delete layer textures
            foreach (int texId in _layerTextureIds)
            {
                GL.DeleteTexture(texId);
            }
            _layerTextureIds.Clear();

            GL.DeleteVertexArray(_spriteVao);
            GL.DeleteVertexArray(_backgroundVao); // Delete background VAO
        }

        static void SetupShaders()
        {
            string vertexShaderSource = @"
            #version 330 core
            layout(location = 0) in vec3 aPosition;
            layout(location = 1) in vec2 aTexCoord;

            uniform vec2 uPositionOffset; // For character and general positioning
            uniform vec2 uScale;
            uniform vec2 uTexOffset;      // For sprite animation and layer texture offset

            out vec2 TexCoord;

            void main()
            {
                vec3 finalPosition = aPosition;
                finalPosition.xy *= uScale; // Apply scaling first
                finalPosition.xy += uPositionOffset; // Then apply position offset
                gl_Position = vec4(finalPosition, 1.0);
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

            if (keyboard.IsKeyDown(Keys.Escape))
            {
                _gameWindowRef.Close();
                return;
            }

            Vector2 moveDirection = Vector2.Zero;
            bool isMoving = false;

            // Comment out vertical movement for now
            /*
            if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up))
            {
                moveDirection.Y = 1;
                currentAnimation = 0; // Assuming animation 0 is 'up'
                isMoving = true;
            }
            if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down))
            {
                moveDirection.Y = -1;
                currentAnimation = 3; // Assuming animation 3 is 'down'
                isMoving = true;
            }
            */
            if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left))
            {
                moveDirection.X = -1;
                currentAnimation = 2; // Assuming animation 2 is 'left' (3rd row)
                isMoving = true;
            }
            if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right))
            {
                moveDirection.X = 1;
                currentAnimation = 1; // Assuming animation 1 is 'right' (2nd row)
                isMoving = true;
            }

            if (moveDirection.LengthSquared > 0)
            {
                 moveDirection.Normalize();
            }

            characterPosition += moveDirection * CharacterSpeed * (float)args.Time;

            _timeSinceLastFrame += args.Time;

            if (isMoving && _timeSinceLastFrame >= TimePerFrame)
            {
                currentFrame = (currentFrame + 1) % nFrames;
                _timeSinceLastFrame -= TimePerFrame;
            }
            else if (!isMoving)
            {
                // Determine idle frame and animation
                if (currentAnimation == 1) // Was moving right (Anim 1)
                {
                    currentFrame = 2; // Idle on 3rd frame of right-facing animation
                }
                else if (currentAnimation == 2) // Was moving left (Anim 2)
                {
                    currentFrame = 2; // Idle on 3rd frame of left-facing animation
                }
                else // Initial state (currentAnimation is 0) or stopped from other animations
                {
                    currentAnimation = 0; // Default to first animation (e.g., front-facing)
                    currentFrame = 0;     // Use the first frame for initial/default idle
                }
                _timeSinceLastFrame = 0;
            }

            offsetS = (float)currentFrame * ds;
            offsetT = (float)currentAnimation * dt;

            // Clamp character X position to stay within screen bounds
            characterPosition.X = MathHelper.Clamp(characterPosition.X, -1.0f + CharacterScale * 0.5f, 1.0f - CharacterScale * 0.5f);
            // Y position is fixed for now (or clamped if you re-enable vertical movement)
            // characterPosition.Y = MathHelper.Clamp(characterPosition.Y, -1.0f + CharacterScale * 0.5f, 1.0f - CharacterScale * 0.5f);

             _gameWindowRef.Title = $"Pos: ({characterPosition.X:F2}, {characterPosition.Y:F2}) Anim: {currentAnimation} Frame: {currentFrame}";
        }

        static void OnRenderFrame(FrameEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(_shaderProgram);

            int posLoc = GL.GetUniformLocation(_shaderProgram, "uPositionOffset");
            int scaleLoc = GL.GetUniformLocation(_shaderProgram, "uScale");
            int texOffsetLoc = GL.GetUniformLocation(_shaderProgram, "uTexOffset");

            // Render Parallax Background Layers (from farthest to nearest)
            GL.BindVertexArray(_backgroundVao);
            for (int i = _layerTextureIds.Count - 1; i >= 0; i--)
            {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, _layerTextureIds[i]);

                // Calculate texture offset for parallax
                // The texture itself will scroll, not the quad
                float parallaxOffsetS = -characterPosition.X * _parallaxFactors[i] * 0.5f; // Multiply by 0.5 because texture coords are 0-1, screen is -1 to 1 for X

                GL.Uniform2(posLoc, Vector2.Zero); // Layers are full screen, no position offset needed for the quad itself
                GL.Uniform2(scaleLoc, Vector2.One); // Layers are full screen
                GL.Uniform2(texOffsetLoc, new Vector2(parallaxOffsetS, 0.0f)); // Apply parallax offset to S coord

                GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            }

            // Render Character Sprite
            GL.BindVertexArray(_spriteVao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _spriteTextureId);

            GL.Uniform2(posLoc, characterPosition);
            GL.Uniform2(scaleLoc, new Vector2(CharacterScale, CharacterScale));
            GL.Uniform2(texOffsetLoc, new Vector2(offsetS, offsetT)); // Sprite sheet animation offsets

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
