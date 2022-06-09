using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using RoR2;
using CharacterMaster = On.RoR2.CharacterMaster;
using Console = System.Console;


namespace RurouArtifacts.ModComponents
{
    

    public class ArtifactOfEgo : ArtifactBase
    {
        
        private class Scheduled
        {
            private float Time { get; }
            private Action Action { get; }

            public Scheduled(float time, Action action)
            {
                Time = time;
                Action = action;
            }

            public bool Ready(float currTime)
            {
                return currTime >= Time;
            }

            public void Execute()
            {
                Action.Invoke();
            }
            

        }

        private Xoroshiro128Plus _egoRng;

        private List<CancellationTokenSource> _source = new();
        private List<CancellationToken> _main = new();
        private List<int> _handled = new();

        private List<Scheduled> _schedule = new ();
        public static ConfigEntry<int> NumberConverted;
        public static ConfigEntry<int> TimeBetweenConversionsInSeconds;
        public override string ArtifactName => "Artifact of Ego";
        public override string ArtifactLangTokenName => "ARTIFACT_OF_EGO";
        public override string ArtifactDescription =>
            "At the beginning of each stage, a random item in your inventory will gain the absorption properties of egocentrism";
        
        public override void Init(ConfigFile config, AssetBundle bundle)
        {
            CreateConfig(config);
            CreateLang();
            var enabledTexture = bundle.LoadAsset<Texture2D>("on_ego");
            var disabledTexture = bundle.LoadAsset<Texture2D>("off_ego");
            ArtifactEnabledIcon = Sprite.Create(enabledTexture, new Rect(0.0f, 0.0f, enabledTexture.width, enabledTexture.height), new Vector2(0.5f, 0.5f));
            ArtifactDisabledIcon = Sprite.Create(disabledTexture, new Rect(0.0f, 0.0f, disabledTexture.width, disabledTexture.height), new Vector2(0.5f, 0.5f));
            CreateArtifact();
            Hooks();
        }

        private void CreateConfig(ConfigFile config)
        {
            NumberConverted = config.Bind("Artifact: " + ArtifactName, "Number of Items Converted", 1, "How many items should be converted each time a conversion occurs");
            TimeBetweenConversionsInSeconds = config.Bind("Artifact: " + ArtifactName, "Time Between Conversions", 60, "Number of seconds between each conversion, timer resets at the beginning of each stage");
        }
        public override void Hooks()
        {
            Run.onServerGameOver += OnGameOver;

            CharacterMaster.OnServerStageBegin += (orig, self, stage) =>
            {
                orig.Invoke(self, stage);
                if (!_handled.Contains(stage.GetInstanceID()))
                {
                    foreach (var source in _source)
                    {
                        try
                        {
                            source.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                            
                        }
                        
                    }
                    _source.Clear();
                    _main.Clear();
                    _handled.Add(stage.GetInstanceID());
                }
                if (self.teamIndex == TeamIndex.Player)
                {
                    OnStageStart(self);
                }
            };
        }

        private void OnGameOver(Run run, GameEndingDef end)
        {
            _schedule.Clear();
            _handled.Clear();
            
            foreach (var pair in _source)
            {
                pair.Cancel();
                pair.Dispose();
            }
            _source.Clear();
            _main.Clear();
        }

        private void OnStageStart( RoR2.CharacterMaster character)
        {
            if (!ArtifactEnabled|| !HasValidItem(character.inventory.itemAcquisitionOrder))
            {
                return;
            }


            var source = new CancellationTokenSource();
            var token = source.Token;

            _source.Add(source);
            _main.Add(token);


            ItemIndex hungry = RandomItemWeighted(character);

            if (hungry == ItemIndex.None)
            {
                return;
            }

            
            Schedule(Run.instance.time+TimeBetweenConversionsInSeconds.Value, () => {Convert(hungry, character, token, source);});


            
            

        }

        private ItemIndex RandomItemWeighted(RoR2.CharacterMaster character, ItemIndex[] exclude = null)
        {
            if (!HasValidItem(character.inventory.itemAcquisitionOrder, exclude))
            {
                return ItemIndex.None;
            }
            List<ItemIndex> items = new List<ItemIndex>(character.inventory.itemAcquisitionOrder);
            _egoRng ??= new Xoroshiro128Plus(Run.instance.seed);
            Util.ShuffleList(items, _egoRng);
            List<ItemIndex> selector = new List<ItemIndex>();

            foreach (ItemIndex item in items.Where(index => ItemIsValid(index, exclude)))
            {
                int count = character.inventory.GetItemCount(item);
                for (int i = 0; i < count; i++)
                {
                    selector.Add(item);
                }
            }

            int selected = _egoRng.RangeInt(0, selector.Count);
            var hungry = selector[selected];
            return hungry;
        }

        private void Convert(ItemIndex hungry, RoR2.CharacterMaster character, CancellationToken token,
            CancellationTokenSource source)
        { 
            if (token.IsCancellationRequested)
            {
                source.Dispose();
                return;
            }

            if (character.bodyInstanceObject is null||!character.bodyInstanceObject.activeInHierarchy)
            {
                source.Cancel();
                source.Dispose();
                return;
            }

            for (int i = 0; i < NumberConverted.Value; i++)
            {
                ItemIndex toRemove = RandomItemWeighted(character, new []{hungry});
                if (toRemove == ItemIndex.None)
                {
                    Schedule(Run.instance.time + (TimeBetweenConversionsInSeconds.Value/10.0f),
                        () => { Convert(hungry, character, token, source); });
                    return;
                }
                character.inventory.RemoveItem(toRemove);
                character.inventory.GiveItem(hungry);
                CharacterMasterNotificationQueue.PushItemTransformNotification(character, toRemove, hungry, CharacterMasterNotificationQueue.TransformationType.LunarSun);
            }
            Schedule(Run.instance.time+TimeBetweenConversionsInSeconds.Value, () => {Convert(hungry, character, token, source);});
            
        }

        private void Schedule(float time, Action action)
        {
            _schedule.Add(new Scheduled(time, action));
        }

        public override void Update()
        {
            foreach (var scheduled in _schedule.Where(scheduled => scheduled.Ready(Run.instance.time)))
            {
                scheduled.Execute();
                _schedule.Remove(scheduled);
            }
        }

        public static bool HasValidItem(IEnumerable<ItemIndex> items, ItemIndex[] exclude = null)
        {
            return items.Any(item => ItemIsValid(item, exclude));
        }

        public static bool ItemIsValid(ItemIndex item, ItemIndex[] exclude = null)
        {
            ItemDef def = ItemCatalog.GetItemDef(item);
            return def.tier != ItemTier.NoTier && def.tier!=ItemTier.AssignedAtRuntime && (exclude == null ||!exclude.Contains(item));
            
        }
    }
}