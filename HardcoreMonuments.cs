using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using System; 

namespace Oxide.Plugins
{
    [Info("HardcoreMonuments/HardcoreMonuments", "Wujaszkun", "1.2.1")]
    [Description("Adds military to harbor")]
    internal class HardcoreMonuments : RustPlugin
    {
        private static HardcoreMonuments instance;
        public BasePlayer player;
        //public HarborData harborData;
        public DynamicConfigFile entityDataFile;
        public List<MonumentInfo> monumentList = new List<MonumentInfo>();
        private ConfigData configData;
        private string containerName;
        private static MonumentInfo currentMonument;
        public static HardcoreMonument hardcoreMonument;

        private void OnServerInitialized()
        {
            try { LoadConfigData(); } catch (Exception e) { instance.Puts("LoadConfigData error " + e.Message); }
            Puts("Loaded done!");
            GetMonumentList();
            instance = this;

            try
            {
                InitializeComponents();
                Puts("Component Initialized!");
            }
            catch (Exception e)
            {
                Puts("Harbor autoinit failed: " + e.Message);
            }
        }

        private void Unload()
        {
            UnloadBuffComponent();
        }

        private void OnServerShutdown()
        {
            UnloadBuffComponent();
        }

        private void UnloadBuffComponent()
        {
            try
            {
                hardcoreMonument.ShowTimer = false;
                hardcoreMonument.DespawnAllEntities();
                hardcoreMonument.DestroyGUI();
                GameManager.Destroy(hardcoreMonument);
            }
            catch (Exception e)
            {
                Puts("OnServerShutdown: " + e.Message);
            }
        }

        #region Chat commands
        [ChatCommand("d_drop")]
        private void sideTimerCommand(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                hardcoreMonument.callDropOnPlayer(player);
            }
        }
        [ChatCommand("d_hidetimer")]
        private void HideTimerCommand(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                hardcoreMonument.ShowTimer = false;
            }
        }
        [ChatCommand("d_showtimer")]
        private void ShowTimerCommand(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                hardcoreMonument.ShowTimer = true;
            }
        }

        //d_time <time_in_seconds> 
        [ChatCommand("d_time")]
        private void SetTimeCommand(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                float newTime = float.Parse(args[0].ToString());
                float oldTime = hardcoreMonument.GetTime();
                if (newTime < 0)
                {
                    PrintToChat(player, "Invalid time, must be greater or equal to 0");
                    return;
                }

                try
                {
                    hardcoreMonument.SetTimer(newTime);
                    PrintToChat(player, $"Time changed {oldTime} -> {newTime}");
                    Puts($"{player.displayName} changed respawn time for {hardcoreMonument.name}:  {oldTime} -> {newTime}");
                }
                catch
                {
                    PrintToChat(player, $"Time changed failed.");
                    Puts($"{player.displayName}: Time changed failed.");
                }
            }
        }

        [ChatCommand("d_list")]
        private void ListMonumentsCommand(BasePlayer player)
        {
            int counter = 0;
            if (player.IsAdmin)
            {
                foreach (var mon in monumentList)
                {
                    counter++;
                    PrintToChat(player, $"{counter}. {mon.name.Basename()}, Tier {mon.Tier.ToString()}, Type {mon.Type.ToString()}");
                }
            }
        }

        [ChatCommand("d_next")]
        private void DespawnCommand(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                UnloadBuffComponent();
                CycleMonumentList();
                InitializeComponents();
            }
        }

        [ChatCommand("d_cycle")]
        private void CycleCommand(BasePlayer player)
        {

            if (player.IsAdmin)
            {
                hardcoreMonument.DespawnAllEntities();
            }
        }

        [ChatCommand("d_spawn")]
        private void SpawnCommand(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                hardcoreMonument.SpawnAllEntities();
            }
        }
        [ChatCommand("d_reset")]
        private void ResetCommand(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                hardcoreMonument.ResetHarborOnDemand();
            }
        }
        [ChatCommand("d_point")]
        private void PointCommand(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                PrintToChat(player, player.transform.InverseTransformPoint(currentMonument.transform.position).ToString());
                PrintToChat(player, currentMonument.transform.InverseTransformPoint(player.transform.position).ToString());
            }
        }
        [ChatCommand("d_help")]
        private void HelpCommand(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                PrintToChat(player, "d_hidetimer -> chowa zegar");
                PrintToChat(player, "d_showtimer -> pokazuje zegar");
                PrintToChat(player, "d_point -> pokazuje koordynaty w stosunku do środka monumentu");
                PrintToChat(player, "d_reset-> resetuje obecnie aktywny monument");
                PrintToChat(player, "d_spawn-> aktywuje monument");
                PrintToChat(player, "d_despawn-> deatywuje monument");
                PrintToChat(player, "d_time <seconds> -> ustawia zegar na zadany czas (w sekundach)");
            }
        }

        [ChatCommand("r")]
        private void RandomStringCommand(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                PrintToChat(player, hardcoreMonument.GetRandomResponse());
            }
        }
        #endregion
        private List<MonumentInfo> GetMonumentList()
        {
            monumentList = new List<MonumentInfo>();

            try
            {
                int iterator = 0;
                foreach (MonumentInfo monInfo in GameObject.FindObjectsOfType<MonumentInfo>())
                {
                    if (monInfo.name.Contains("harbor") || monInfo.name.Contains("large"))
                    {
                        if (monInfo.Type != MonumentType.Cave)
                        {
                            Puts(iterator + ". " + monInfo.Type.ToString() + " " + monInfo.Tier.ToString() + " " + monInfo.name.ToString());
                            iterator++;
                            monumentList.Add(monInfo);
                        }
                    }
                }

                return monumentList;
            }
            catch (Exception e)
            {
                Puts("GetMonument failed!" + e.Message);
                return null;
            }
        }
        private void InitializeComponents()
        {

            if (GetMonumentList() == null)
            {

                Puts("Initialize components failed: No monuments found!");
                return;
            }
            try
            {
                if (currentMonument == null) currentMonument = monumentList[0];

                if (currentMonument.GetComponent<HardcoreMonument>() == null)
                {
                    hardcoreMonument = currentMonument.gameObject.AddComponent<HardcoreMonument>();
                }
                foreach (var admin in BasePlayer.activePlayerList)
                {
                    if (admin.IsAdmin) PrintToChat(admin, $"Hardcore monument started: {hardcoreMonument} at {hardcoreMonument.transform.position}");
                    if (admin.IsAdmin) PrintToChat(admin, $"Next Hardcore monument: {monumentList[1]} at {monumentList[1].transform.position}");
                }
            }
            catch
            {

            }
        }

        private void CycleMonumentList()
        {
            monumentList.Add(monumentList[0]);
            monumentList.RemoveAt(0);
            
        }
        #region ControlGUI

        public void ShowControlGUI(BasePlayer player)
        {
            if (!player.IsAdmin) return;


            CuiElementContainer mainContainer = new CuiElementContainer();

            CuiPanel mainPanel = new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.6" },
                RectTransform = { AnchorMin = "0.2 0.2", AnchorMax = "0.8 0.8" },
                CursorEnabled = true
            };

            CuiPanel listPanel = new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.8" },
                RectTransform = { AnchorMin = "0.6 0.25", AnchorMax = "0.75 0.75" }
            };
            CuiLabel mainLabel = new CuiLabel
            {
                RectTransform = { AnchorMin = "0.6 0.76", AnchorMax = "0.75 0.79" },
                Text = { Color = "1 1 1 1", Text = "Upcoming events list", Align = TextAnchor.MiddleCenter }
            };

            CuiButton closeWindowButton = new CuiButton
            {
                Button =
                {
                    Command = "hidecontrol",
                },
                Text = { Color = "0 0 0 1", Text = "Close Window", Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = "0.25 0.25", AnchorMax = "0.35 0.30" }
            };

            mainContainer.Add(mainPanel, "Hud", $"mainPanel_{player.net.ID.ToString()}");
            mainContainer.Add(mainLabel, "Hud", $"mainLabel_{player.net.ID.ToString()}");
            mainContainer.Add(listPanel, "Hud", $"listPanel_{player.net.ID.ToString()}");
            mainContainer.Add(closeWindowButton, "Hud", $"closeButton_{player.net.ID.ToString()}");

            CuiHelper.AddUi(player, mainContainer);
        }

        public void HideControlGUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, containerName);
            CuiHelper.DestroyUi(player, $"mainPanel_{player.net.ID.ToString()}");
            CuiHelper.DestroyUi(player, $"mainLabel_{player.net.ID.ToString()}");
            CuiHelper.DestroyUi(player, $"listPanel_{player.net.ID.ToString()}");
            CuiHelper.DestroyUi(player, $"closeButton_{player.net.ID.ToString()}");
        }

        #endregion

        #region ConfigData
        public void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        protected override void LoadDefaultConfig()
        {
            ConfigData config = new ConfigData
            {
                SpawnCargoShip = true,
                SpawnBradley = true,
                SpawnStrongPoints = true,
                SpawnMilitaryPersonnel = true,
                SpawnAdvancedCrates = false,
                SpawnSAMSites = true,
                SpawnAmmoCrates = false,
                SpawnHackableCrates = true,
                RespawnTime = 21600f,
                ShowTimer = true
            };
            SaveConfig(config);
        }
        private void LoadConfigData()
        {
            try
            {
                LoadConfigFromFile();
                Puts("Configuration has been loaded succesfully!");
            }
            catch
            {
                LoadDefaultConfig();
                Puts("Config load failed. Loaded default configuration!");
            }
        }
        private void LoadConfigFromFile()
        {
            configData = Config.ReadObject<ConfigData>();
        }

        public class ConfigData
        {
            [JsonProperty(PropertyName = "Spawn Carbo Ship")]
            public bool SpawnCargoShip { get; set; }

            [JsonProperty(PropertyName = "Spawn Bradley")]
            public bool SpawnBradley { get; set; }

            [JsonProperty(PropertyName = "Spawn Strong Points")]
            public bool SpawnStrongPoints { get; set; }

            [JsonProperty(PropertyName = "Spawn Military Personnel")]
            public bool SpawnMilitaryPersonnel { get; set; }

            [JsonProperty(PropertyName = "Spawn Advanced Crates")]
            public bool SpawnAdvancedCrates { get; set; }

            [JsonProperty(PropertyName = "Spawn SAM Sites")]
            public bool SpawnSAMSites { get; set; }

            [JsonProperty(PropertyName = "Spawn Ammo Crates")]
            public bool SpawnAmmoCrates { get; set; }

            [JsonProperty(PropertyName = "Spawn Hackable Crates")]
            public bool SpawnHackableCrates { get; set; }

            [JsonProperty(PropertyName = "Respawn Time")]
            public float RespawnTime { get; set; }

            [JsonProperty(PropertyName = "Show Timer")]
            public bool ShowTimer { get; set; }
        }
        #endregion

        #region OxideHooks
        private bool CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        {
            if (entity is HTNPlayer || entity is CH47HelicopterAIController)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            foreach (MonumentInfo harbor in monumentList)
            {
                try
                {
                    HardcoreMonument hardcoreMonument = harbor.GetComponent<HardcoreMonument>();
                    if (hardcoreMonument != null)
                    {
                        hardcoreMonument.DestroyGUI(player);
                    }
                }
                catch (Exception e)
                {
                    Puts("OnPlayerDisconnected failed: " + e.Message);
                }
            }
        }

        void OnCrateHack(HackableLockedCrate crate)
        {
            PrintToChat(player, "OnCrateHack triggered");

            try
            {
                if (hardcoreMonument != null)
                {
                    if (hardcoreMonument.HackCrateList.Contains(crate))
                    {
                        PrintToChat("Patrol Copter Inbound! Get cover!");
                        if (hardcoreMonument.GetRandomResponse() == "UH1")
                        {
                            hardcoreMonument.CallPatrollHelicopter(crate);
                        }
                        else
                        {
                            hardcoreMonument.callDropOnPlayer(crate);
                        }
                    }
                }
            }
            catch
            {
                Puts("OnCrateHack failed");
            }
        }
        object OnNpcTarget(BaseNpc npc, BaseEntity entity)
        {
            if (entity.GetComponent<CH47HelicopterAIController>() != null)
            {
                return true;

            }
            else
            {
                return null;
            }
        }
        #endregion

        #region BuffTimer class
        public class BuffTimer : MonoBehaviour
        {
            private ConfigData configData;

            public float TimeLeft { get; private set; }
            private bool timerIsActive;
            private float consoleUpdateInterval;
            private float nextActionTime;

            private void Awake()
            {
                configData = instance.configData;
                TimeLeft = configData.RespawnTime;
                timerIsActive = false;
                consoleUpdateInterval = 300f;
            }

            private void FixedUpdate()
            {
                try
                {
                    if (TimeLeft > 0 && TimerIsActive())
                    {
                        TimeLeft -= Time.deltaTime;
                    }
                    else if (TimeLeft == 0)
                    {
                        Deactivate();
                    }
                }
                catch (Exception e)
                {
                    instance.Puts("Timer fixedUpdate()" + e.Message);
                }

                if (Time.time > nextActionTime)
                {
                    nextActionTime = Time.time + consoleUpdateInterval;

                    instance.Puts($"{hardcoreMonument.name} reset countdown time: {this.TimeLeft}");
                }
            }
            public void Activate()
            {
                timerIsActive = true;
            }
            public void Activate(float time)
            {
                TimeLeft = time;
                timerIsActive = true;
            }
            public void Deactivate()
            {
                timerIsActive = false;
            }
            public bool TimerIsActive()
            {
                return timerIsActive;
            }
            public float GetCurrentTimer()
            {
                return TimeLeft;
            }
            public float ResetTimer(float time)
            {
                TimeLeft = time;
                return TimeLeft;
            }
            public float ResetTimerAndDeactivate(float time)
            {
                Deactivate();
                TimeLeft = time;
                return TimeLeft;
            }
            public float SetTimer(float time)
            {
                TimeLeft = time;
                return TimeLeft;
            }
        }
        #endregion

        #region HarborBuff class
        public class HardcoreMonument : MonoBehaviour
        {
            public static BradleyAPC bradley;
            private static CargoShip cargoShip;
            public static BuffTimer respawnTimer;
            private static ConfigData configData;
            private static MonumentInfo harborInfo;

            private static Vector3 harborPosition;
            private static Quaternion harborRotation;

            public Dictionary<uint, string> spawnedEntityList = new Dictionary<uint, string>();
            public Dictionary<uint, BaseEntity> spawnedBaseEntityList = new Dictionary<uint, BaseEntity>();
            public Dictionary<Vector3, Vector3> soldiersPositionRotation = new Dictionary<Vector3, Vector3>();

            private readonly List<Vector3> path;
            private List<Vector3> spawnPoints = new List<Vector3>();
            private readonly List<BaseEntity> projectileList = new List<BaseEntity>();
            private readonly List<BaseEntity> flareList = new List<BaseEntity>();
            private List<BaseEntity> militaryPersonnelList;
            private CuiElementContainer container;

            private readonly string samPrefab = "assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab";
            private readonly string flarePrefab = "assets/prefabs/tools/flareold/flare.deployed.prefab";
            private readonly string scientistPrefab = "assets/prefabs/npc/scientist/htn/scientist_full_any.prefab";
            private readonly string hackableCratePrefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
            private readonly string watchTowerPrefab = "assets/prefabs/building/watchtower.wood/watchtower.wood.prefab";
            private readonly string barricadePrefab = "assets/prefabs/deployable/barricades/barricade.concrete.prefab";
            private readonly string metalWirePrefab = "assets/prefabs/deployable/barricades/barricade.metal.prefab";

            public bool ShowTimer { get; set; }
            public List<BaseEntity> HackCrateList { get; private set; }

            private readonly TOD_Sky Sky = TOD_Sky.Instance;
            private float nextActionTime;
            private float period;

            #region Hooks
            private void Awake()
            {
                instance.Puts("Component awake!");
                try
                {
                    nextActionTime = 0.0f;
                    period = 120f;

                    ShowTimer = true;
                    respawnTimer = gameObject.AddComponent<BuffTimer>();
                    harborInfo = gameObject.GetComponent<MonumentInfo>();
                    harborPosition = harborInfo.transform.position;
                    harborRotation = harborInfo.transform.rotation;
                    configData = instance.configData;

                    militaryPersonnelList = new List<BaseEntity>();
                    spawnPoints = new List<Vector3>();

                    respawnTimer.Activate();

                    SpawnAllEntities();
                }
                catch (Exception e)
                {
                    instance.Puts("Awake failed" + e.Message);
                }
            }
            public Vector3 GetRandomPoint(Vector3 center, float maxX, float maxZ)
            {
                Vector3 newPointRelative = new Vector3(UnityEngine.Random.Range(-maxX, maxX), 0.1f, UnityEngine.Random.Range(maxZ, maxZ));
                Vector3 newPointAbsolute = harborInfo.transform.TransformPoint(newPointRelative);
                return newPointAbsolute;
            }

            public List<Vector3> GetRandomPointsList(int numberOfPoints, int range)
            {
                List<Vector3> tempList = new List<Vector3>();

                for (int i = 0; i < numberOfPoints; i++)
                {
                    try
                    {
                        Vector3 pos = GetRandomPoint(harborInfo.transform.position, -range, range);

                        if (!Physics.CheckSphere(pos, 2f, LayerMask.GetMask("Terrain", "World")))
                        {
                            tempList.Add(pos);
                        }
                        else
                        {
                            i--;
                        }
                    }
                    catch (Exception e)
                    {
                        instance.Puts("Get point failed: " + e.Message);
                    }
                }
                return tempList;
            }
            public void ResetHarborOnDemand()
            {
                DespawnAllEntities();
                spawnedEntityList.Clear();
                spawnedBaseEntityList.Clear();
                soldiersPositionRotation.Clear();
                SpawnAllEntities();
                respawnTimer.ResetTimer(configData.RespawnTime);
            }
            public void ResetHarbor()
            {
                if (respawnTimer != null && respawnTimer.GetCurrentTimer() == 0)
                {
                    ResetHarborOnDemand();
                }
            }

            private void FixedUpdate()
            {
                if (component != null)
                {
                    instance.PrintToChat((component.transform.position - harborInfo.transform.position).ToString());
                }
                if (patrolHelicopterAI != null)
                {
                    isPatrolHelicopterSpawned = patrolHelicopterAI.isDead == true ? false : true;
                    if (Vector3.Distance(patrolHelicopterAI.transform.position, harborInfo.transform.position) > 70)
                    {
                        patrolHelicopterAI.State_Move_Enter(harborInfo.transform.position);
                    }
                    if (Vector3.Distance(patrolHelicopterAI.transform.position, harborInfo.transform.position) < 50)
                    {

                    }
                }
                else
                {
                    isPatrolHelicopterSpawned = false;
                }

                try
                {
                    DestroyGUIInactive();
                    if (respawnTimer != null && ShowTimer == true) UpdateGUI();
                }
                catch (Exception e)
                {
                    instance.Puts("Update GUI failed! " + e.Message);
                }
                ResetHarbor();
                try
                {
                    if (cargoShip != null)
                    {
                        cargoShip.targetNodeIndex = -1;
                    }
                    else
                    {

                    }
                }
                catch (Exception e)
                {
                    instance.Puts("CargoShip path fixedUpdate() " + e.Message);
                }
                try
                {
                    if (bradley != null && path != null)
                    {
                        if (bradley.PathComplete())
                        {
                            path.Reverse();
                            bradley.currentPath = path;
                        }
                    }
                }
                catch (Exception e)
                {
                    instance.Puts("Bradley path fixedUpdate() " + e.Message);
                }
                try
                {
                    if (projectileList.Count > 0)
                    {
                        foreach (BaseEntity proj in projectileList)
                        {
                            if (proj != null && proj.GetComponent<ServerProjectile>()._currentVelocity.y < -0.01f)
                            {
                                flareList.Add(CreateFlare(proj.transform.position));
                                proj.GetComponent<TimedExplosive>().Explode();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    instance.Puts("Flare projectile launch failed: " + e.Message);
                }
                try
                {
                    if (flareList.Count > 0)
                    {
                        foreach (BaseEntity flare in flareList)
                        {
                            if (flare != null && flare.GetComponent<Rigidbody>().velocity.y < -0.5f)
                            {
                                Vector3 velocity = new Vector3(UnityEngine.Random.Range(-2f, 2f), -0.01f, UnityEngine.Random.Range(-2f, 2f));
                                flare.GetComponent<Rigidbody>().velocity = velocity;
                            }

                        }
                    }
                }
                catch (Exception e)
                {
                    instance.Puts("Flare ignition failed: " + e.Message);
                }
                try
                {
                    if (Time.time > nextActionTime)
                    {
                        nextActionTime = Time.time + period;
                        if (Sky.IsNight)
                        {
                            instance.Puts("Flares deployed");
                            instance.Puts("Current time: " + Time.time);
                            instance.Puts("Is night: " + Sky.IsNight);
                            DeployFlare();
                        }
                    }
                }
                catch (Exception e)
                {
                    instance.Puts("Flare ignition failed: " + e.Message);
                }
            }
            #endregion

            #region DespawnEntities
            public void DespawnAllEntities()
            {
                respawnTimer.ResetTimer(configData.RespawnTime);
                foreach (KeyValuePair<uint, BaseEntity> entity in spawnedBaseEntityList)
                {
                    try
                    {
                        entity.Value.Kill();
                        spawnedEntityList.Remove(entity.Key);
                    }
                    catch (Exception e)
                    {
                        instance.Puts("Couldn't delete entity " + entity.Key + " " + e.ToString());
                    }
                }
            }
            #endregion

            #region Spawn Entities
            public void SpawnAllEntities()
            {
                if (harborInfo != null)
                {
                    instance.Puts("Spawning entities");

                    try { spawnedBaseEntityList.Clear(); instance.Puts("spawnedBaseEntityList cleared "); }

                    catch (Exception e) { instance.Puts("spawnedBaseEntityList clear or null" + e.Message); }

                    if (configData.SpawnCargoShip)
                    {
                        try { SpawnShip(); } catch (Exception e) { instance.Puts("Ship spawn error " + e.Message); }
                    }

                    if (configData.SpawnBradley)
                    {
                        //try { SpawnBradley(); } catch (Exception e) { instance.Puts("Bradley spawn error " + e.Message); }
                    }

                    if (configData.SpawnStrongPoints)
                    {
                        //try { SpawnOutposts(); } catch (Exception e) { instance.Puts("Outpost spawn error " + e.Message); }
                    }

                    if (configData.SpawnMilitaryPersonnel)
                    {
                        //try { SpawnMilitaryPersonnel(); } catch (Exception e) { instance.Puts("Soldiers spawn error " + e.Message); }
                    }

                    if (configData.SpawnSAMSites)
                    {
                        try { SpawnSAMSites(); } catch (Exception e) { instance.Puts("SAMSites spawn error " + e.Message); }
                    }

                    if (configData.SpawnAdvancedCrates)
                    {
                        try { SpawnAdvancedCrates(); } catch (Exception e) { instance.Puts("AdvCrates spawn error " + e.Message); }
                    }

                    if (configData.SpawnAmmoCrates)
                    {
                        try { SpawnAmmoCrates(); } catch (Exception e) { instance.Puts("AmmoCrates spawn error " + e.Message); }
                    }

                    if (configData.SpawnHackableCrates)
                    {
                        try { SpawnHackableCrates(); } catch (Exception e) { instance.Puts("HackCrates spawn error " + e.Message); }
                    }
                }
                else
                {
                    instance.Puts("No monument !!!");
                }
            }
            private void RotateObject(BaseEntity entity, bool rotateInner)
            {
                Quaternion monumentRotation = harborInfo.transform.rotation;
                Vector3 centerPoint = harborInfo.transform.position;

                entity.transform.RotateAround(centerPoint, Vector3.up, monumentRotation.eulerAngles.y + 62f);
                if (rotateInner)
                {
                    entity.transform.rotation = monumentRotation;
                }
            }
            private void SpawnBradley()
            {
                instance.Puts("Spawning Bradley");

                if (harborInfo != null)
                {
                    List<Vector3> path = new List<Vector3>
                    {
                        harborInfo.transform.TransformPoint(new Vector3(107f, 5.3f, -21f)),
                        harborInfo.transform.TransformPoint(new Vector3(33.6f, 5.2f, -21f)),
                        harborInfo.transform.TransformPoint(new Vector3(15f, 5.2f, -36.6f)),
                        harborInfo.transform.TransformPoint(new Vector3(-115f, 5.2f, -36f)),
                        harborInfo.transform.TransformPoint(new Vector3(15f, 5.2f, -36.6f)),
                        harborInfo.transform.TransformPoint(new Vector3(33.6f, 5.2f, -21f))
                    };

                    BaseEntity entity = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", harborInfo.transform.TransformPoint(new Vector3(107f, 5.3f, -21f)), Quaternion.identity, true);
                    bradley = entity.GetComponent<BradleyAPC>();
                    bradley.currentPath = path;
                    bradley.UpdateMovement_Patrol();
                    bradley.Spawn();

                    AddEntityToData(entity);
                }
                else
                {
                    instance.Puts("Monument is null");
                }
            }
            private void SpawnOutposts()
            {
                instance.Puts("Spawning Strongpoints");
                List<Vector3> outpostPositions = new List<Vector3>
                {
                    new Vector3(-30f, 5.1f, -18f),
                    new Vector3(-30f, 5.1f, -92f),
                    new Vector3(32f, 5.1f, -60f),
                    new Vector3(116f, 5.1f, -45f),
                    new Vector3(-123f, 5.1f, -53f)
                };
                foreach (Vector3 position in outpostPositions)
                {
                    SpawnOutpostEntity(position, harborInfo, 7.5f, true, false, true);
                }
            }
            private void SpawnMilitaryPersonnel()
            {
                instance.Puts("Spawning Military Personnel");
                int counter = 0;
                for (int i = 0; i < 20; i++)
                {
                    var distance = 50f;
                    var x = Core.Random.Range(-distance, distance);
                    var z = Core.Random.Range(-distance, distance);

                    BaseEntity scientist = GameManager.server.CreateEntity(scientistPrefab, harborInfo.transform.TransformPoint(new Vector3(x, 0.1f, z)), harborInfo.transform.rotation, true);
                    scientist?.Spawn();
                    AddEntityToData(scientist);
                    counter++;
                }
                instance.Puts($"Spawned {counter} military personnel units!");
            }

            private void SpawnSAMSites()
            {
                instance.Puts("Spawning SAM sites");
                List<Vector3> SAMSitesPositions = new List<Vector3>
                {
                    new Vector3(2.5f, 29.5f, -18.7f)
                };

                foreach (Vector3 position in SAMSitesPositions)
                {
                    BaseEntity samSite = SpawnEntity(harborInfo.transform.TransformPoint(position), new Vector3(0, 0, 0), samPrefab, false);
                    samSite.GetComponent<SamSite>().isLootable = true;
                    samSite.GetComponent<SamSite>().UpdateHasPower(100, 1);
                    samSite.GetComponent<SamSite>().inventory.AddItem(ItemManager.FindItemDefinition("ammo.rocket.sam"), 200);
                }

                SAMSitesPositions.Clear();

            }

            private void SpawnAdvancedCrates()
            {
                //instance.Puts("Spawning advanced crates");

                //foreach (Vector3 point in GetRandomPointsList(3, 100))
                //{
                //    BaseEntity loot = SpawnEntity(point + new Vector3(0, -3f, 0), Vector3.zero, advancedCratePrefab, false);
                //    loot.GetComponent<LootContainer>().SpawnType = LootContainer.spawnType.CRASHSITE;
                //    loot.GetComponent<LootContainer>().maxDefinitionsToSpawn = 10;
                //    loot.GetComponent<LootContainer>().minSecondsBetweenRefresh = 8000;
                //}

            }
            private void SpawnAmmoCrates()
            {
                //instance.Puts("Spawning ammo crates");

                //foreach (Vector3 point in GetRandomPointsList(3, 100))
                //{
                //    BaseEntity ammoCrate = SpawnEntity(point + new Vector3(0, -3f, 0), Vector3.zero, ammoCratePrefab, false);
                //    ammoCrate.GetComponent<LootContainer>().minSecondsBetweenRefresh = 8000;
                //}
            }
            private void SpawnHackableCrates()
            {
                if (harborInfo == null)
                {
                    instance.Puts("No monument");
                    return;
                }
                HackCrateList = new List<BaseEntity>();

                BaseEntity entity = GameManager.server.CreateEntity(hackableCratePrefab, harborInfo.transform.TransformPoint(new Vector3(UnityEngine.Random.Range(-50f, 50f), 10f, UnityEngine.Random.Range(-70f, 70f))), harborInfo.transform.rotation, true);
                entity?.Spawn();
                AddEntityToData(entity);
                HackCrateList.Add(entity);

                BaseEntity entity2 = GameManager.server.CreateEntity(hackableCratePrefab, harborInfo.transform.TransformPoint(new Vector3(UnityEngine.Random.Range(-50f, 50f), 10f, UnityEngine.Random.Range(-70f, 70f))), harborInfo.transform.rotation, true);
                entity2?.Spawn();
                AddEntityToData(entity2);
                HackCrateList.Add(entity2);

                BaseEntity entity3 = GameManager.server.CreateEntity(hackableCratePrefab, harborInfo.transform.TransformPoint(new Vector3(UnityEngine.Random.Range(-50f, 50f), 10f, UnityEngine.Random.Range(-70f, 70f))), harborInfo.transform.rotation, true);
                entity3?.Spawn();
                AddEntityToData(entity3);
                HackCrateList.Add(entity3);

                entity?.GetComponent<HackableLockedCrate>().CreateMapMarker(15f);
                entity2?.GetComponent<HackableLockedCrate>().CreateMapMarker(15f);
                entity3?.GetComponent<HackableLockedCrate>().CreateMapMarker(15f);
            }
            private void SpawnShip()
            {
                var shipPosition = harborInfo.transform.TransformPoint(new Vector3(35f, 0f, 168f));
                var shipRotation = Quaternion.Euler(harborInfo.transform.rotation.eulerAngles + new Vector3(0f, 180f, 0f));

                BaseEntity entity = GameManager.server.CreateEntity("assets/content/vehicles/boats/cargoship/cargoshiptest.prefab", shipPosition, shipRotation, true);
                entity.Spawn();
                cargoShip = entity.GetComponent<CargoShip>();
                cargoShip.SpawnSubEntities();

                AddEntityToData(entity);
                SpawnShipTurrets(entity);
            }
            private void SpawnShipTurrets(BaseEntity shipEntity)
            {
                //right side
                List<Vector3> shipTurretsList = new List<Vector3>
                {
                    new Vector3(12f, 5f, -10f),
                    new Vector3(12f, 5f, 0f),
                    new Vector3(12f, 5f, 10f),
                    new Vector3(12f, 5f, 20f),
                    new Vector3(12f, 5f, 30f)
                };
                string turretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
                foreach (Vector3 point in shipTurretsList)
                {
                    //spawn turrents 
                    BaseEntity turret = GameManager.server.CreateEntity(turretPrefab, shipEntity.transform.TransformPoint(point), Quaternion.Euler(shipEntity.transform.rotation.eulerAngles + new Vector3(0f, 0, -90f)), true);
                    turret?.Spawn();

                    AutoTurret autoTurret = turret.GetComponent<AutoTurret>();
                    autoTurret.inventory.AddItem(ItemManager.FindItemDefinition("ammo.rifle"), 100000);
                    autoTurret.isLootable = false;

                    try { autoTurret.UpdateFromInput(100, 1); } catch { }
                    AddEntityToData(turret);
                }

                shipTurretsList = new List<Vector3>
                {
                    new Vector3(-12f, 5f, -10f),
                    new Vector3(-12f, 5f, 0f),
                    new Vector3(-12f, 5f, 10f),
                    new Vector3(-12f, 5f, 20f),
                    new Vector3(-12f, 5f, 30f)
                };

                foreach (Vector3 point in shipTurretsList)
                {
                    Quaternion.Euler(new Vector3(0f, 0f, 90f));
                    BaseEntity turret = GameManager.server.CreateEntity(turretPrefab, shipEntity.transform.TransformPoint(point), Quaternion.Euler(shipEntity.transform.rotation.eulerAngles + new Vector3(0f, 0, 90f)), true);
                    turret?.Spawn();

                    AutoTurret autoTurret = turret.GetComponent<AutoTurret>();
                    autoTurret.inventory.AddItem(ItemManager.FindItemDefinition("ammo.rifle"), 100000);
                    autoTurret.isLootable = false;

                    try { autoTurret.UpdateFromInput(100, 1); } catch { }
                    AddEntityToData(turret);
                }
            }

            private void SpawnOutpostEntity(Vector3 centerPoint, MonumentInfo monumentInfo, float size, bool spawnAmmoCrates, bool spawnAdvancedCrates, bool spawnSamSites)
            {
                BaseEntity towerEntity = GameManager.server.CreateEntity(watchTowerPrefab, monumentInfo.transform.TransformPoint(centerPoint), monumentInfo.transform.rotation, true);
                towerEntity.Spawn();
                AddEntityToData(towerEntity);

                float x = 3;
                float y = 0;
                float z = 3;

                // parent version
                List<Vector3> sandBagsPositions = new List<Vector3>
                {
                    new Vector3(x * 1, y, size),
                    new Vector3(x * 2, y, size),
                    new Vector3(x * -1, y, size),
                    new Vector3(x * -2, y, size),

                    new Vector3(x * 1, y, -size),
                    new Vector3(x * 2, y, -size),
                    new Vector3(x * -1, y, -size),
                    new Vector3(x * -2, y, -size)
                };

                List<Vector3> metalWirePositions = new List<Vector3>
                {
                    new Vector3(x * 1, y, size + 1.5f),
                    new Vector3(x * 2, y, size + 1.5f),
                    new Vector3(x * -1, y, size + 1.5f),
                    new Vector3(x * -2, y, size + 1.5f),

                    new Vector3(x * 1, y, -size - 1.5f),
                    new Vector3(x * 2, y, -size - 1.5f),
                    new Vector3(x * -1, y, -size - 1.5f),
                    new Vector3(x * -2, y, -size - 1.5f)
                };

                SpawnEntityFromList(sandBagsPositions, new Vector3(0, 0, 0), barricadePrefab, towerEntity, true);
                SpawnEntityFromList(metalWirePositions, new Vector3(0, 0, 0), metalWirePrefab, towerEntity, true);

                sandBagsPositions = new List<Vector3>
                {
                    new Vector3(size, y, z * 1),
                    new Vector3(size, y, z * 2),
                    new Vector3(size, y, z * -1),
                    new Vector3(size, y, z * -2),

                    new Vector3(-size, y, z * 1),
                    new Vector3(-size, y, z * 2),
                    new Vector3(-size, y, z * -1),
                    new Vector3(-size, y, z * -2)
                };

                metalWirePositions = new List<Vector3>
                {
                    new Vector3(size + 1.5f, y, z * 1),
                    new Vector3(size + 1.5f, y, z * 2),
                    new Vector3(size + 1.5f, y, z * -1),
                    new Vector3(size + 1.5f, y, z * -2),

                    new Vector3(-size - 1.5f, y, z * 1),
                    new Vector3(-size - 1.5f, y, z * 2),
                    new Vector3(-size - 1.5f, y, z * -1),
                    new Vector3(-size - 1.5f, y, z * -2)
                };

                SpawnEntityFromList(sandBagsPositions, new Vector3(0f, 90f, 0f), barricadePrefab, towerEntity, true);
                SpawnEntityFromList(metalWirePositions, new Vector3(0f, 90f, 0f), metalWirePrefab, towerEntity, true);
                float addY = 1.25f;
                y += addY;

                sandBagsPositions = new List<Vector3>
                {
                    new Vector3(x * 1, y, size),
                    new Vector3(x * -1, y, size),

                    new Vector3(x * 1, y, -size),
                    new Vector3(x * -1, y, -size)
                };
                SpawnEntityFromList(sandBagsPositions, new Vector3(0f, 0f, 0f), barricadePrefab, towerEntity, true);

                sandBagsPositions = new List<Vector3>
                {
                    new Vector3(size, y, z * 1f),
                    new Vector3(size, y, z * -1f),

                    new Vector3(-size, y, z * 1f),
                    new Vector3(-size, y, z * -1f)
                };

                SpawnEntityFromList(sandBagsPositions, new Vector3(0f, 90f, 0f), barricadePrefab, towerEntity, true);
                y -= addY;
                if (spawnAmmoCrates)
                {
                    //List<Vector3> cratesLocations = new List<Vector3>
                    //{
                    //    new Vector3(size - 1, y, -size + 4),
                    //    new Vector3(size - 1, y, -size + 2),
                    //    new Vector3(size - 1, y, -size + 3)
                    //};

                    //SpawnEntityFromList(cratesLocations, new Vector3(0, 0, 0), ammoCratePrefab, towerEntity, true);
                }
                if (spawnSamSites)
                {
                    List<Vector3> samPositions = new List<Vector3>
                    {
                        new Vector3(-5f, y, -5f),
                        new Vector3(5f, y, -5f)
                    };

                    SpawnEntityFromList(samPositions, Vector3.zero, samPrefab, towerEntity, true);
                }

                float offset = 4f;

                List<Vector3> gunnerPositions = new List<Vector3>
                {
                    new Vector3(0f, 7.3f, 0f),
                    new Vector3(offset, y, offset),
                    new Vector3(-offset, y, offset),
                    new Vector3(offset, y, -offset),
                    new Vector3(-offset, y, -offset)
                };

                SpawnEntityFromList(gunnerPositions, Vector3.zero, scientistPrefab, towerEntity, true);
            }
            private void SpawnEntityFromList(List<Vector3> list, Vector3 rot, string prefab, BaseEntity parentEntity, bool setAsActive)
            {
                foreach (Vector3 point in list)
                {

                    Vector3 pos = parentEntity.transform.TransformPoint(point);
                    Quaternion rotation = new Quaternion
                    {
                        eulerAngles = parentEntity.transform.rotation.eulerAngles + rot
                    };

                    BaseEntity entity = GameManager.server.CreateEntity(prefab, pos, rotation, setAsActive);
                    if (entity.GetComponent<Barricade>() != null)
                    {
                        entity.GetComponent<Barricade>().canNpcSmash = false;
                        entity.GetComponent<Barricade>().reflectDamage = 0f;
                        entity.GetComponent<Barricade>().Spawn();
                        AddEntityToData(entity.GetComponent<Barricade>());
                    }
                    else
                    {
                        entity.Spawn();
                    }

                    AddEntityToData(entity);
                }
            }
            private BaseEntity SpawnEntity(Vector3 pos, Vector3 rot, string prefab, bool rotateInner)
            {
                Quaternion rotNew = new Quaternion
                {
                    eulerAngles = rot
                };

                BaseEntity entity = GameManager.server.CreateEntity(prefab, pos, rotNew, true);

                RotateObject(entity, rotateInner);
                entity.Spawn();
                AddEntityToData(entity);
                return entity;
            }
            private BaseEntity SpawnEntityMilitary(Vector3 pos, string prefab, bool rotateInner)
            {
                BaseEntity entity = GameManager.server.CreateEntity(prefab, pos, Quaternion.identity, true);

                RotateObject(entity, rotateInner);
                entity.Spawn();

                militaryPersonnelList.Add(entity);
                AddEntityToData(entity);
                return entity;
            }
            #endregion


            private void AddEntityToData(BaseEntity entity)
            {
                if (!spawnedEntityList.ContainsKey(entity.net.ID))
                {
                    spawnedEntityList.Add(entity.net.ID, entity.ShortPrefabName);
                }
                if (!spawnedBaseEntityList.ContainsKey(entity.net.ID))
                {
                    spawnedBaseEntityList.Add(entity.net.ID, entity);
                }
            }

            public void DeployFlare()
            {
                string projectile = "assets/prefabs/ammo/40mmgrenade/40mm_grenade_he.prefab";
                foreach (Vector3 position in FlareSpawnPoints())
                {
                    BaseEntity projectilet = GameManager.server.CreateEntity(projectile, position);
                    if (projectilet != null)
                    {
                        projectilet.SendMessage("InitializeVelocity", Vector3.up * 30f);
                        projectilet.Spawn();
                        projectileList.Add(projectilet);
                    }
                }
            }

            private List<Vector3> FlareSpawnPoints()
            {
                if (spawnPoints.Count == 0)
                {
                    foreach (KeyValuePair<uint, BaseEntity> entity in spawnedBaseEntityList)
                    {
                        if (entity.Value.ShortPrefabName.Contains("scientist"))
                        {
                            Vector3 updatedPoint = new Vector3(entity.Value.transform.position.x, entity.Value.transform.position.y + 0.25f, entity.Value.transform.position.z);
                            spawnPoints.Add(updatedPoint);
                        }
                    }
                }
                return spawnPoints;
            }

            private BaseEntity CreateFlare(Vector3 position)
            {
                BaseEntity ent = GameManager.server.CreateEntity(flarePrefab, position, new Quaternion(), true);
                ent?.Spawn();

                LightEx projectileLight = ent.gameObject.AddComponent<LightEx>();
                projectileLight.colorA = Color.green;
                projectileLight.colorB = Color.green;
                projectileLight.alterColor = false;
                projectileLight.randomOffset = true;
                projectileLight.transform.parent = ent.transform;
                return ent;
            }
            #region GUI
            public void ShowGUI()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    container = new CuiElementContainer();
                    if (respawnTimer != null)
                    {
                        TimeSpan timeSpan = TimeSpan.FromSeconds(respawnTimer.GetCurrentTimer());
                        int hr = timeSpan.Hours;
                        int mn = timeSpan.Minutes;
                        int sec = timeSpan.Seconds;

                        string anchorMaxDynamic = GetCuiSizeMax(0.199f, 0.048f, respawnTimer.GetCurrentTimer());

                        CuiLabel text = new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0.02 0.02", AnchorMax = "0.2 0.05" },
                            Text = { Text = " Harbor respawn in: " + hr + ":" + mn + ":" + sec, FontSize = 14, Color = "1 1 1 1", Align = TextAnchor.MiddleLeft }
                        };

                        container.Add(text, "Hud", "text_" + player.net.ID.ToString());
                    }

                    CuiHelper.AddUi(player, container);
                }
            }
            private string GetCuiSizeMax(float maxx, float maxy, float time)
            {
                float startTime = configData.RespawnTime;
                float newMaxx = (time / startTime) * maxx;
                string anchorMax = newMaxx.ToString() + " " + maxy.ToString();

                return anchorMax;
            }
            public void DestroyGUI()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, "background_" + player.net.ID.ToString());
                    CuiHelper.DestroyUi(player, "progressBar_" + player.net.ID.ToString());
                    CuiHelper.DestroyUi(player, "text_" + player.net.ID.ToString());
                }
            }

            public void DestroyGUI(BasePlayer player)
            {
                if (player != null)
                {
                    CuiHelper.DestroyUi(player, "background_" + player.net.ID.ToString());
                    CuiHelper.DestroyUi(player, "progressBar_" + player.net.ID.ToString());
                    CuiHelper.DestroyUi(player, "text_" + player.net.ID.ToString());
                }
                else
                {

                }
            }
            public void DestroyGUIInactive()
            {
                foreach (BasePlayer player in BasePlayer.sleepingPlayerList)
                {
                    CuiHelper.DestroyUi(player, "background_" + player.net.ID.ToString());
                    CuiHelper.DestroyUi(player, "progressBar_" + player.net.ID.ToString());
                    CuiHelper.DestroyUi(player, "text_" + player.net.ID.ToString());
                }
            }
            public void UpdateGUI()
            {
                DestroyGUI();
                if (ShowTimer == true)
                {
                    ShowGUI();
                }
            }
            public CH47ReinforcementListener listener;
            private PatrolHelicopterAI patrolHelicopterAI;
            private bool isPatrolHelicopterSpawned;
            private CH47HelicopterAIController component;
            private Vector3 LZ;

            internal void CallPatrollHelicopter(HackableLockedCrate crate)
            {
                try
                {
                    var spawnPoint = harborInfo.transform.position + new Vector3(50f, 100f, 0f);
                    if (isPatrolHelicopterSpawned == false)
                    {
                        string copterPrefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";
                        BaseEntity entity = GameManager.server.CreateEntity(copterPrefab);
                        patrolHelicopterAI = entity.GetComponent<PatrolHelicopterAI>();
                        patrolHelicopterAI.destination = crate.transform.position;
                        patrolHelicopterAI.MoveToDestination();
                        entity?.Spawn();
                        isPatrolHelicopterSpawned = true;
                    }
                }
                catch
                {
                    instance.PrintToChat("CallPatrollHelicopter failed");
                }
            }

            public void callDropOnPlayer(BaseEntity crate)
            {
                var startDist = 100f;
                var point = harborInfo.transform.position + new Vector3(22f, 6f, 20f);
                LZ = point;
                try
                {
                    var landingZone = hardcoreMonument.gameObject.AddComponent<CH47LandingZone>();
                    landingZone.transform.localPosition = point;
                    landingZone.dropoffScale = 5;
                }
                catch (Exception e)
                {
                    instance.Puts(e.Message);
                    instance.Puts(e.StackTrace);
                }

                var entity = GameManager.server.CreateEntity("assets/prefabs/npc/ch47/ch47scientists.entity.prefab",
                    new Vector3(), new Quaternion(), true);

                component = entity.GetComponent<CH47HelicopterAIController>();

                if (component != null)
                {
                    component.transform.position = new Vector3(-startDist, 50f, 100) + LZ;
                    component?.Spawn();
                    component?.SetLandingTarget(LZ);
                }
            }

            public string GetRandomResponse()
            {

                List<String> responses = new List<String> { "UH1", "UH1", "UH1" };
                int index = new System.Random().Next(0, responses.Count - 1);
                string response = responses[index];

                return response;
            }
            public void SetTimer(float time)
            {
                respawnTimer.SetTimer(time);
            }

            internal float GetTime()
            {
                return respawnTimer.GetCurrentTimer();
            }
            #endregion
        }
        #endregion
    }
    class CustomMonumentInfo : MonumentInfo
    {
        internal CustomMonumentInfo(MonumentInfo monument)
        {
            this.Position = monument.transform.position;
            this.Name = monument.name;
            this.Info = monument;

            IsArmed = (monument.GetComponent<HardcoreMonuments>() != null) ? true : false;
        }

        public bool IsArmed { get; private set; }
        public object Position { get; private set; }
        public string Name { get; private set; }
        public MonumentInfo Info { get; private set; }
    }
}