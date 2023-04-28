using Vintagestory.API.Common;
using Vintagestory.API.Client;
using Vintagestory.GameContent.Mechanics;

public class StirlingAgeMod : ModSystem {
    public override void Start(ICoreAPI api) {
        base.Start(api);
        api.RegisterBlockClass("StirlingEngineBurner", typeof(BlockStirlingEngineBurner));
        api.RegisterBlockClass("StirlingEngineRotor", typeof(BlockStirlingEngineRotor));
        api.RegisterBlockEntityClass("StirlingEngineBurner", typeof(BlockEntityStirlingEngineBurner));
        api.RegisterBlockEntityBehaviorClass("StirlingEngineRotor", typeof(BEBehaviorStirlingEngineRotor));
        if(api.World is IClientWorldAccessor) {
            MechanicalPowerMod mpmod = api.ModLoader.GetModSystem<MechanicalPowerMod>();
            if(!MechNetworkRenderer.RendererByCode.ContainsKey("stirlingenginerotor")) {
                MechNetworkRenderer.RendererByCode.Add("stirlingenginerotor",typeof(StirlingEngineRotorRenderer));
            }
        }
    }
}
