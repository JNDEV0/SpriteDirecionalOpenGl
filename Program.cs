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
        private const float CharacterSpeed = 1.5f;
        private const float CharacterScale = 0.2f;

        private static Stopwatch _timer = new Stopwatch();
        private static double _timeSinceLastFrame = 0.0;
        private const double AnimationFps = 12.0;
        private const double TimePerFrame = 1.0 / AnimationFps;

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

        static void OnLoad()
        {
            GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _spriteVao = CreateSpriteQuad();
            SetupShaders();

            try
            {
                _spriteTextureId = TextureLoader.LoadTexture(SpriteSheetPath);
            }
            catch (Exception ex)
            {
                _gameWindowRef?.Close();
            }

            _timer.Start();
        }

        static void OnUnload()
        {
            _timer.Stop();

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DeleteBuffer(_spriteVbo);

            GL.UseProgram(0);
            GL.DeleteProgram(_shaderProgram);

            GL.DeleteTexture(_spriteTextureId);

            GL.DeleteVertexArray(_spriteVao);
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

            if (keyboard.IsKeyDown(Keys.Escape))
            {
                _gameWindowRef.Close();
                return;
            }

            Vector2 moveDirection = Vector2.Zero;
            bool isMoving = false;

            if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up))
            {
                moveDirection.Y = 1;
                currentAnimation = 0;
                isMoving = true;
            }
            if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down))
            {
                moveDirection.Y = -1;
                currentAnimation = 3;
                isMoving = true;
            }
            if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left))
            {
                moveDirection.X = -1;
                currentAnimation = 2;
                isMoving = true;
            }
            if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right))
            {
                moveDirection.X = 1;
                currentAnimation = 1;
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
                currentFrame = 0;
                _timeSinceLastFrame = 0;
            }

            offsetS = (float)currentFrame * ds;
            offsetT = (float)currentAnimation * dt;

            characterPosition.X = MathHelper.Clamp(characterPosition.X, -1.0f + CharacterScale * 0.5f, 1.0f - CharacterScale * 0.5f);
            characterPosition.Y = MathHelper.Clamp(characterPosition.Y, -1.0f + CharacterScale * 0.5f, 1.0f - CharacterScale * 0.5f);

             _gameWindowRef.Title = $"Pos: ({characterPosition.X:F2}, {characterPosition.Y:F2}) Anim: {currentAnimation} Frame: {currentFrame}";
        }

        static void OnRenderFrame(FrameEventArgs args)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(_shaderProgram);
            GL.BindVertexArray(_spriteVao);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _spriteTextureId);

            int posLoc = GL.GetUniformLocation(_shaderProgram, "uPositionOffset");
            int scaleLoc = GL.GetUniformLocation(_shaderProgram, "uScale");
            int texOffsetLoc = GL.GetUniformLocation(_shaderProgram, "uTexOffset");

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
