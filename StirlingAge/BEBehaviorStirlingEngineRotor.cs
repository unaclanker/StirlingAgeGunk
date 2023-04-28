/* based on BEBehaviorWindmillRotor from vssurvivalmod */

using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

public class BEBehaviorStirlingEngineRotor : BEBehaviorMPRotor {
    float target_torque;

    const float BASIS_TEMP = 700.0f;
    const float ΤORQUE_AT_BASIS_TEMP = 0.25f;

    protected override float Resistance => 0.002f;
    protected override double AccelerationFactor => 0.05d;
    protected override float TargetSpeed => 0.5f;
    protected override float TorqueFactor => target_torque;

    BlockEntity our_entity;
    IStirlingBurner burner;

    public BEBehaviorStirlingEngineRotor(BlockEntity blockentity) : base(blockentity) {
        our_entity = blockentity;
        burner = null; // defer finding the burner
    }

    public override void Initialize(ICoreAPI api, JsonObject properties) {
        base.Initialize(api, properties);
        Blockentity.RegisterGameTickListener(UpdateMech, 1000);
    }

    private void UpdateMech(float dt) {
        if(burner == null) {
            BlockPos down_pos = our_entity.Pos.DownCopy();
            burner = our_entity.Api.World.BlockAccessor.GetBlockEntity(down_pos) as IStirlingBurner;
            if(burner == null) {
                // hopefully it will appear soon...
                target_torque = 0.0f;
                return;
            }
        }
        // float world_temperature = api.World.BlockAccessor.GetClimateAt(entity.Pos.AsBlockPos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, api.World.Calendar.TotalDays).Temperature;
        target_torque = (burner.HotSideTemperature - burner.ColdSideTemperature) * ΤORQUE_AT_BASIS_TEMP / BASIS_TEMP;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb) {
        base.GetBlockInfo(forPlayer, sb);
        if(burner == null) {
            sb.AppendLine(Lang.Get("Temperature: {0}°C", "???"));
        }
        else {
            sb.AppendLine(Lang.Get("Temperature: {0}°C", (int)burner.HotSideTemperature));
        }
        sb.AppendLine(Lang.Get("Max Torque: {0} kNm", (int)(target_torque * 20 / 0.25)));
        if(network == null) {
            sb.AppendLine(Lang.Get("Speed: {0} rpm", 0));
        }
        else {
            sb.AppendLine(Lang.Get("Speed: {0} rpm", (int)(network.Speed * 48 + 0.5)));
        }
    }
}
