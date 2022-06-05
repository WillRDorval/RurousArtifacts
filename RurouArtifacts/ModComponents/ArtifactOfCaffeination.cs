using System.Linq;
using BepInEx.Configuration;
using RoR2;
using RoR2.ExpansionManagement;
using UnityEngine;

namespace RurouArtifacts.ModComponents;

public class ArtifactOfCaffeination : ArtifactBase
{
    public override string ArtifactName => "Artifact of Caffeination";
    public override string ArtifactLangTokenName => "ARTIFACT_OF_CAFFEINATION";
    public override string ArtifactDescription => "You get a free mocha at the start of each stage, but so does everyone else (requires void expansion)";
    
    public override bool ArtifactEnabled
    {
        get
        {
            var exp  = ExpansionCatalog.expansionDefs.FirstOrDefault(x => x.nameToken == "DLC1_NAME");
            return RunArtifactManager.instance.IsArtifactEnabled(ArtifactDef)&&exp!=null&&Run.instance.IsExpansionEnabled(exp);
        }
    }

    private static ItemIndex _mocha = ItemIndex.None;

    public static  ItemIndex MochaIndex
    {
        get
        {
            if (_mocha == ItemIndex.None)
            {
                _mocha = ItemCatalog.FindItemIndex("AttackSpeedAndMoveSpeed");
            }

            return _mocha;
        }
    }

    

    private static int _numberMochas;
    
    private static ConfigEntry<int> _mochasPerStage;
    public override void Init(ConfigFile config, AssetBundle bundle)
    {
        CreateConfig(config);
        CreateLang();
        var enabledTexture = bundle.LoadAsset<Texture2D>("ArtifactOfCaffeinationIconEnabled");
        var disabledTexture = bundle.LoadAsset<Texture2D>("ArtifactOfCaffeinationIconDisabled");
        ArtifactEnabledIcon = Sprite.Create(enabledTexture, new Rect(0.0f, 0.0f, enabledTexture.width, enabledTexture.height), new Vector2(0.5f, 0.5f));
        ArtifactDisabledIcon = Sprite.Create(disabledTexture, new Rect(0.0f, 0.0f, disabledTexture.width, disabledTexture.height), new Vector2(0.5f, 0.5f));
        CreateArtifact();
        Hooks();
    }

    private void CreateConfig(ConfigFile config)
    {
        _mochasPerStage = config.Bind("Artifact: " + ArtifactName, "Number of mochas per stage",
            1, "How many mochas you (and the enemies) get each stage");
    }

    public override void Hooks()
    {
        CharacterMaster.onStartGlobal += master =>
        {
            if (ArtifactEnabled && master.teamIndex != TeamIndex.Player)
            {
                GiveMochas(master);
            }
        };

        Run.onRunStartGlobal += _ =>
        {
            _numberMochas = 0;
        };
        
        On.RoR2.CharacterMaster.OnServerStageBegin += (orig, self, stage) =>
        {
            orig.Invoke(self, stage);
            if (ArtifactEnabled&&self.teamIndex == TeamIndex.Player)
            {
                OnStageStart(self);
            }
        };
    }

    private static void OnStageStart(CharacterMaster self)
    {
        _numberMochas += _mochasPerStage.Value;
        self.inventory.GiveItem(MochaIndex, _mochasPerStage.Value);
        CharacterMasterNotificationQueue.PushItemNotification(self, MochaIndex);
    }

    private static void GiveMochas(CharacterMaster characterMaster)
    {
        characterMaster.inventory.GiveItem(MochaIndex, _numberMochas);
    }

    public override void Update()
    {
        
    }
}