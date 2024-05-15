using Vintagestory.API.Client;
using Vintagestory.API.MathTools;


namespace Bulwark {
    public class VerticalFlagRenderer : IRenderer {

        protected readonly ICoreClientAPI api;
        protected readonly MeshRef meshRef;
        protected readonly BlockPos pos;

        public Matrixf ModelMat = new();
        

        public VerticalFlagRenderer(
            ICoreClientAPI coreClientAPI,
            MeshData       mesh,
            BlockPos       pos
        ) {

            this.api         = coreClientAPI;
            this.meshRef     = coreClientAPI.Render.UploadMesh(mesh);
            this.pos         = pos;

        } // VerticalFlagRenderer ..


        public double RenderOrder => 0.5;
        public int    RenderRange => 24;


        public virtual void OnRenderFrame(
            float deltaTime,
            EnumRenderStage stage
        ) {

            if (this.meshRef == null) return;
            float ellapsedSeconds = this.api.ElapsedMilliseconds * 0.001f;

            IRenderAPI rpi = this.api.Render;
            Vec3d camPos   = this.api.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(this.pos.X, this.pos.Y, this.pos.Z);
            prog.Tex2D = api.ItemTextureAtlas.AtlasTextures[0].TextureId;

            prog.ModelMatrix = ModelMat
                .Identity()
                .Translate(this.pos.X - camPos.X, this.pos.Y - camPos.Y, this.pos.Z - camPos.Z)
                .Translate(0f, 20f / 16f, 7.5f / 16f)
                .Scale(1f, 1f + GameMath.Sin(ellapsedSeconds) * 0.05f, 1f + GameMath.Sin(ellapsedSeconds * 4f) * 0.5f)
                .Values;

            prog.ViewMatrix       = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;
            rpi.RenderMesh(meshRef);
            prog.Stop();

        } // void ..



        public void Dispose() {

            this.api.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
            this.meshRef.Dispose();

        } // void ..
    } // class ..
} // namespace ..
