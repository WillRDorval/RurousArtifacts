using System.Collections.Generic;
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

    private static  ItemIndex MochaIndex
    {
        get
        {
            if (_mocha == ItemIndex.None)
            {
                _mocha = ItemCatalog.FindItemIndex(_mochaItemName.Value);
            }

            return _mocha;
        }
    }

    

    private static int _numberMochas;
    
    private static ConfigEntry<int> _mochasPerStage;
    private static ConfigEntry<string> _mochaItemName;
    private static ConfigEntry<bool> _scaleEnemiesByPlayers;
    private static ConfigEntry<bool> _givePlayerItems;
    private List<int> _handled = new();

    public override void Init(ConfigFile config, AssetBundle bundle)
    {
        CreateConfig(config);
        CreateLang();
        var enabledTexture = bundle.LoadAsset<Texture2D>("on_coffee");
        var disabledTexture = bundle.LoadAsset<Texture2D>("off_coffee");
        ArtifactEnabledIcon = Sprite.Create(enabledTexture, new Rect(0.0f, 0.0f, enabledTexture.width, enabledTexture.height), new Vector2(0.5f, 0.5f));
        ArtifactDisabledIcon = Sprite.Create(disabledTexture, new Rect(0.0f, 0.0f, disabledTexture.width, disabledTexture.height), new Vector2(0.5f, 0.5f));
        CreateArtifact();
        Hooks();
    }

    private void CreateConfig(ConfigFile config)
    {
        _mochasPerStage = config.Bind("Artifact: " + ArtifactName, "Number of mochas per stage",
            1, "How many mochas you (and the enemies) get each stage");
        _mochaItemName = config.Bind("Artifact: " + ArtifactName, "Name of mocha item",
            "AttackSpeedAndMoveSpeed", 
            "The in code name of the item to give to players (change at your own risk)");
        _scaleEnemiesByPlayers = config.Bind("Artifact: " + ArtifactName, "Scale enemies by number of players",
            false, "Causes enemies to scale faster by the number of players (i.e. 2 times as many mochas in 2 player)");
        _givePlayerItems = config.Bind("Artifact: " + ArtifactName, "Give player Mochas",
            true, "Disable to give no items to the player(s) but still give them to enemies");
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
            _handled.Clear();
            _numberMochas = 0;
        };
        
        On.RoR2.CharacterMaster.OnServerStageBegin += (orig, self, stage) =>
        {
            orig.Invoke(self, stage);
            if (!_handled.Contains(stage.GetInstanceID()))
            {
                _numberMochas += _mochasPerStage.Value*(_scaleEnemiesByPlayers.Value?Run.instance.participatingPlayerCount:1);
                _handled.Add(stage.GetInstanceID());
            }
            if (ArtifactEnabled&&self.teamIndex == TeamIndex.Player)
            {
                OnStageStart(self);
            }
        };
    }

    private static void OnStageStart(CharacterMaster self)
    {
        if (!_givePlayerItems.Value) return;
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