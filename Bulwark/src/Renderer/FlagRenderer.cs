using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using System;


namespace Bulwark {
    public class FlagRenderer : IRenderer {

        protected readonly ICoreClientAPI api;
        protected readonly MeshRef meshRef;
        protected readonly BlockPos pos;
        protected readonly BlockEntityBehaviorFlag blockEntity;
        protected readonly float poleTop;
        protected readonly float poleBottom;

        public Matrixf ModelMat = new();
        

        public FlagRenderer(
            ICoreClientAPI coreClientAPI,
            MeshData       mesh,
            BlockPos       pos,
            BlockEntityBehaviorFlag blockEntity,
            float          poleTop,
            float          poleBottom
        ) {

            this.api         = coreClientAPI;
            this.meshRef     = coreClientAPI.Render.UploadMesh(mesh);
            this.pos         = pos;
            this.blockEntity = blockEntity;
            this.poleTop     = poleTop;
            this.poleBottom  = poleBottom;

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
                .Translate(0.5f, this.poleBottom + (this.poleTop - this.poleBottom) * this.blockEntity.CapturedPercent, 0.5f)
                .RotateY(MathF.Atan2(GlobalConstants.CurrentWindSpeedClient.Z, GlobalConstants.CurrentWindSpeedClient.X) * MathF.Sign(GlobalConstants.CurrentWindSpeedClient.X))
                .Scale(1f + GameMath.Cos(ellapsedSeconds * 1.1f) * 0.1f, 1f + GameMath.Sin(ellapsedSeconds) * 0.05f, 1f + GameMath.Sin(ellapsedSeconds * 4f) * 0.5f)
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
