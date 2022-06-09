using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using RoR2;
using RoR2.Artifacts;
using RoR2.ExpansionManagement;
using RoR2.UI;
using UnityEngine;
using UnityEngine.Networking;
using EnemyInfoPanelInventoryProvider = On.RoR2.EnemyInfoPanelInventoryProvider;

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

    private Inventory EvolutionInv => MonsterTeamGainsItemsArtifactManager.monsterTeamInventory;
    private static Inventory CaffeineInv;


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
        NetworkedInventoryPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/MonsterTeamGainsItemsArtifactInventory");
        Hooks();
    }

    private static GameObject NetworkedInventoryPrefab { get; set; }

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
        SpawnCard.onSpawnedServerGlobal += OnSpawnCardGlobal;
        Run.onRunStartGlobal += OnRunStartGlobal;
        Run.onRunDestroyGlobal += OnRunDestroyGlobal;

        On.RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.OnRunStartGlobal += (orig, run) =>
        {
            if (!RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.monsterTeamGainsItemsArtifactDef))
            {
                return;
            }

            orig(run);
        };

        On.RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.OnServerStageBegin += (orig, stage) =>
        {
            if (!RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.monsterTeamGainsItemsArtifactDef))
            {
                return;
            }

            orig(stage);
        };

        On.RoR2.Artifacts.MonsterTeamGainsItemsArtifactManager.OnPrePopulateSceneServer += (orig, director) =>
        {
            if (!RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.monsterTeamGainsItemsArtifactDef))
            {
                return;
            }

            orig(director);
        };

        EnemyInfoPanelInventoryProvider.Awake += (orig, self) =>
        {
            orig(self);
            self.enabled = true;
        };

        EnemyInfoPanelInventoryProvider.OnInventoryChanged += (orig, self) =>
        {
            orig(self);
        };

        On.RoR2.UI.EnemyInfoPanel.RefreshHUD += (orig, hud) =>
        {
            orig(hud);
        };

        Stage.onServerStageBegin += stage =>
        {
            if (!_handled.Contains(stage.GetInstanceID())&& ArtifactEnabled)
            {
                if (RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.monsterTeamGainsItemsArtifactDef))
                {
                    EvolutionInv.GiveItem(_mocha, _mochasPerStage.Value *
                        (_scaleEnemiesByPlayers.Value ? Run.instance.participatingPlayerCount : 1));
                    EnemyInfoPanel.RefreshAll();
                }
                else
                {
                    CaffeineInv.GiveItem(_mocha, _mochasPerStage.Value *
                        (_scaleEnemiesByPlayers.Value ? Run.instance.participatingPlayerCount : 1));
                    var instancesList = InstanceTracker.GetInstancesList<RoR2.EnemyInfoPanelInventoryProvider>();
                    foreach (var instance in instancesList)
                    {
                        if (instance.inventory == CaffeineInv)
                        {
                            instance.MarkAsDirty();
                
                        }
                    }
                }
                _handled.Add(stage.GetInstanceID());
            }
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

    private void OnRunStartGlobal(Run run)
    {
        if (!NetworkServer.active||!ArtifactEnabled) return;
        CaffeineInv = Object.Instantiate(NetworkedInventoryPrefab).GetComponent<Inventory>();
        CaffeineInv.GetComponent<TeamFilter>().teamIndex = TeamIndex.Monster;
        NetworkServer.Spawn(CaffeineInv.gameObject);

    }

    private static void OnRunDestroyGlobal(Run run)
    {
        if (CaffeineInv)
        {
            NetworkServer.Destroy(CaffeineInv.gameObject);
        }

        CaffeineInv = null;
    }

    private void OnSpawnCardGlobal(SpawnCard.SpawnResult result)
    {
        if (RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.monsterTeamGainsItemsArtifactDef) ||
            !ArtifactEnabled || !NetworkServer.active) return;
        var characterMaster = result.spawnedInstance ? result.spawnedInstance.GetComponent<CharacterMaster>() : null;
        if (!characterMaster)
        {
            return;
        }
        if (characterMaster.teamIndex != TeamIndex.Monster)
        {
            return;
        }

        characterMaster.inventory.AddItemsFrom(CaffeineInv);
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