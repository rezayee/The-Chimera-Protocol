#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

public static class Level1Builder
{
    private static Material matMarble;
    private static Material matWalnut;
    private static Material matDirtyTiles;
    private static Material matMetal;
    private static Material matConcrete;
    private static Material matGlass;
    private static Material matRedEmissive;
    private static Material matCRTEmissive;
    private static Material matDecalBloodPool;
    private static Material matDecalBloodSplatter;
    private static Material matDecalHazard;

    [MenuItem("The Chimera Protocol/Build Level 1 Environment")]
    public static void BuildLevel1()
    {
        // 1. Create or open new scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        LoadMaterials();

        // Root hierarchy
        var rootLevel = new GameObject("Level1_UpperAdministration");
        var groupArch = new GameObject("01_Architecture"); groupArch.transform.SetParent(rootLevel.transform);
        var groupProps = new GameObject("02_Props_And_Furniture"); groupProps.transform.SetParent(rootLevel.transform);
        var groupLights = new GameObject("03_Lighting_And_Atmosphere"); groupLights.transform.SetParent(rootLevel.transform);
        var groupDecals = new GameObject("04_Decals_And_Gore"); groupDecals.transform.SetParent(rootLevel.transform);
        var groupAudio = new GameObject("05_Audio_And_Triggers"); groupAudio.transform.SetParent(rootLevel.transform);

        // 2. Build Architecture
        BuildLobby(groupArch.transform, groupProps.transform, groupLights.transform, groupDecals.transform);
        BuildSecurityRoom(groupArch.transform, groupProps.transform, groupLights.transform, groupDecals.transform);
        BuildClinic(groupArch.transform, groupProps.transform, groupLights.transform, groupDecals.transform);
        BuildEntrance(groupArch.transform, groupProps.transform, groupLights.transform);
        BuildCorridorsAndElevator(groupArch.transform, groupProps.transform, groupLights.transform, groupDecals.transform);

        // 3. Setup Global Atmosphere, Lighting & Post-Processing
        SetupEnvironmentLighting(groupLights.transform);

        // 4. Setup Main Camera with cinematic framing
        SetupCamera(rootLevel.transform);

        // 5. Save Scene
        string scenePath = "Assets/_Project/Scenes/Level1_UpperAdministration.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log("Level 1 Upper Administration successfully built and saved to " + scenePath);
    }

    private static void LoadMaterials()
    {
        string mDir = "Assets/_Project/Environments/Level1_Admin/Materials/";
        string dDir = "Assets/_Project/Environments/Level1_Admin/Decals/";

        matMarble = AssetDatabase.LoadAssetAtPath<Material>(mDir + "M_Marble_Floor.mat");
        matWalnut = AssetDatabase.LoadAssetAtPath<Material>(mDir + "M_Walnut_Veneer.mat");
        matDirtyTiles = AssetDatabase.LoadAssetAtPath<Material>(mDir + "M_Dirty_Tiles.mat");
        matMetal = AssetDatabase.LoadAssetAtPath<Material>(mDir + "M_Metal_Plate.mat");
        matConcrete = AssetDatabase.LoadAssetAtPath<Material>(mDir + "M_Concrete_Wall.mat");
        matGlass = AssetDatabase.LoadAssetAtPath<Material>(mDir + "M_Shattered_Glass.mat");
        matRedEmissive = AssetDatabase.LoadAssetAtPath<Material>(mDir + "M_Emergency_Red_Emissive.mat");
        matCRTEmissive = AssetDatabase.LoadAssetAtPath<Material>(mDir + "M_CRT_Monitor_Emissive.mat");

        matDecalBloodPool = AssetDatabase.LoadAssetAtPath<Material>(dDir + "M_Decal_BloodPool.mat");
        matDecalBloodSplatter = AssetDatabase.LoadAssetAtPath<Material>(dDir + "M_Decal_BloodSplatter.mat");
        matDecalHazard = AssetDatabase.LoadAssetAtPath<Material>(dDir + "M_Decal_HazardStripe.mat");
    }

    // --- LOBBY (20x20m, H: 5m) ---
    private static void BuildLobby(Transform arch, Transform props, Transform lights, Transform decals)
    {
        var lobbyObj = new GameObject("Room_Lobby"); lobbyObj.transform.SetParent(arch);

        // Floor: 20x20m Polished Marble
        CreateBox(lobbyObj.transform, "Floor_Lobby_Marble", new Vector3(0, -0.1f, 0), new Vector3(20, 0.2f, 20), matMarble);

        // Ceiling: 20x20m Dropped acoustic ceiling at Y=5m
        CreateBox(lobbyObj.transform, "Ceiling_Lobby", new Vector3(0, 5.1f, 0), new Vector3(20, 0.2f, 20), matConcrete);

        // Walls: Height 5m
        // North Wall (with opening for Entrance at center: entrance width 10m, so left wall 5m, right wall 5m)
        CreateBox(lobbyObj.transform, "Wall_Lobby_North_Left", new Vector3(-7.5f, 2.5f, 10f), new Vector3(5f, 5f, 0.4f), matWalnut);
        CreateBox(lobbyObj.transform, "Wall_Lobby_North_Right", new Vector3(7.5f, 2.5f, 10f), new Vector3(5f, 5f, 0.4f), matWalnut);
        // Header beam above entrance
        CreateBox(lobbyObj.transform, "Wall_Lobby_North_Header", new Vector3(0f, 4.5f, 10f), new Vector3(10f, 1f, 0.4f), matWalnut);

        // South Wall (with opening for South Corridor at center: corridor width 10m)
        CreateBox(lobbyObj.transform, "Wall_Lobby_South_Left", new Vector3(-7.5f, 2.5f, -10f), new Vector3(5f, 5f, 0.4f), matWalnut);
        CreateBox(lobbyObj.transform, "Wall_Lobby_South_Right", new Vector3(7.5f, 2.5f, -10f), new Vector3(5f, 5f, 0.4f), matWalnut);
        CreateBox(lobbyObj.transform, "Wall_Lobby_South_Header", new Vector3(0f, 4.5f, -10f), new Vector3(10f, 1f, 0.4f), matWalnut);

        // East Wall (opening to Security at center: Z = -2.5 to 2.5)
        CreateBox(lobbyObj.transform, "Wall_Lobby_East_Top", new Vector3(10f, 2.5f, 6.25f), new Vector3(0.4f, 5f, 7.5f), matWalnut);
        CreateBox(lobbyObj.transform, "Wall_Lobby_East_Bottom", new Vector3(10f, 2.5f, -6.25f), new Vector3(0.4f, 5f, 7.5f), matWalnut);
        CreateBox(lobbyObj.transform, "Wall_Lobby_East_Header", new Vector3(10f, 4.25f, 0f), new Vector3(0.4f, 1.5f, 5f), matWalnut);

        // West Wall (opening to Clinic at Z = 2 to 6, and opening to Restrooms at Z = -6 to -2)
        CreateBox(lobbyObj.transform, "Wall_Lobby_West_Top", new Vector3(-10f, 2.5f, 8f), new Vector3(0.4f, 5f, 4f), matWalnut);
        CreateBox(lobbyObj.transform, "Wall_Lobby_West_Mid", new Vector3(-10f, 2.5f, 0f), new Vector3(0.4f, 5f, 4f), matWalnut);
        CreateBox(lobbyObj.transform, "Wall_Lobby_West_Bottom", new Vector3(-10f, 2.5f, -8f), new Vector3(0.4f, 5f, 4f), matWalnut);
        CreateBox(lobbyObj.transform, "Wall_Lobby_West_Header1", new Vector3(-10f, 4.25f, 4f), new Vector3(0.4f, 1.5f, 4f), matWalnut);
        CreateBox(lobbyObj.transform, "Wall_Lobby_West_Header2", new Vector3(-10f, 4.25f, -4f), new Vector3(0.4f, 1.5f, 4f), matWalnut);

        // Architectural Columns (Grand Sci-Fi corporate pillars)
        CreateColumn(lobbyObj.transform, new Vector3(-5f, 2.5f, 5f), 5f, 0.8f, matConcrete, matMetal);
        CreateColumn(lobbyObj.transform, new Vector3(5f, 2.5f, 5f), 5f, 0.8f, matConcrete, matMetal);
        CreateColumn(lobbyObj.transform, new Vector3(-5f, 2.5f, -5f), 5f, 0.8f, matConcrete, matMetal);
        CreateColumn(lobbyObj.transform, new Vector3(5f, 2.5f, -5f), 5f, 0.8f, matConcrete, matMetal);

        // Hero Asset: Shattered Dark Mahogany Reception Desk with Curved Counter & Broken Glass
        BuildReceptionDesk(props, new Vector3(0, 0, 1.5f));

        // Overturned Designer Leather Armchairs & Waiting Area
        BuildWaitingArea(props, new Vector3(-6f, 0, -2f));

        // Broken Double Glass Doors at South Exit
        BuildBrokenGlassDoors(props, new Vector3(0, 0, -9.9f));

        // Fallen Acoustic Ceiling Tiles & Hanging Conduit Cables
        BuildFallenCeilingDebris(props, new Vector3(2f, 0, 0));

        // Lighting:
        // 1. Warm flickering LED strip above reception
        var stripObj = new GameObject("Light_Lobby_Reception_Flicker");
        stripObj.transform.SetParent(lights);
        stripObj.transform.position = new Vector3(0, 4.6f, 1.5f);
        var stripLight = stripObj.AddComponent<Light>();
        stripLight.type = LightType.Spot;
        stripLight.spotAngle = 100f;
        stripLight.innerSpotAngle = 60f;
        stripLight.range = 8f;
        stripLight.color = new Color(1.0f, 0.75f, 0.45f); // Warm incandescent
        stripLight.intensity = 2.2f;
        stripLight.shadows = LightShadows.Soft;
        stripObj.transform.rotation = Quaternion.Euler(90, 0, 0);

        var flicker = stripObj.AddComponent<FlickeringLight>();
        flicker.minIntensity = 0.2f;
        flicker.maxIntensity = 2.5f;
        flicker.outageChance = 0.25f;

        // 2. Cold moonlight coming from high shattered windows
        var moonObj = new GameObject("Light_Lobby_Moonlight_Bleed");
        moonObj.transform.SetParent(lights);
        moonObj.transform.position = new Vector3(-7f, 4.8f, 9.5f);
        var moonLight = moonObj.AddComponent<Light>();
        moonLight.type = LightType.Spot;
        moonLight.spotAngle = 80f;
        moonLight.range = 14f;
        moonLight.color = new Color(0.35f, 0.65f, 1.0f); // Cold moonlight
        moonLight.intensity = 3.5f;
        moonLight.shadows = LightShadows.Soft;
        moonObj.transform.rotation = Quaternion.Euler(65, 35, 0);

        // Reflection Probe for shiny marble reflections
        var probeObj = new GameObject("ReflectionProbe_Lobby");
        probeObj.transform.SetParent(lights);
        probeObj.transform.position = new Vector3(0, 2.0f, 0);
        var probe = probeObj.AddComponent<ReflectionProbe>();
        probe.size = new Vector3(22, 6, 22);
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;

        // Decals: Blood pool near reception desk
        CreateDecalProjector(decals, "Decal_Lobby_BloodPool", new Vector3(0.5f, 0.05f, 0.8f), new Vector3(0, 0, 0), new Vector3(4.5f, 4.5f, 2f), matDecalBloodPool);
        CreateDecalProjector(decals, "Decal_Lobby_BloodTrail", new Vector3(-3.5f, 0.05f, 2.5f), new Vector3(0, 45, 0), new Vector3(3.2f, 3.2f, 2f), matDecalBloodSplatter);
    }

    // --- SECURITY ROOM (15x15m, H: 3.5m) ---
    private static void BuildSecurityRoom(Transform arch, Transform props, Transform lights, Transform decals)
    {
        var secObj = new GameObject("Room_Security"); secObj.transform.SetParent(arch);
        Vector3 center = new Vector3(17.5f, 0, 0);

        // Floor: 15x15m Metal plate
        CreateBox(secObj.transform, "Floor_Security_Metal", center + new Vector3(0, -0.1f, 0), new Vector3(15, 0.2f, 15), matMetal);

        // Ceiling: 15x15m Concrete at Y=3.5m
        CreateBox(secObj.transform, "Ceiling_Security", center + new Vector3(0, 3.6f, 0), new Vector3(15, 0.2f, 15), matConcrete);

        // Perimeter Walls (Concrete & acoustic tiles)
        // North Wall
        CreateBox(secObj.transform, "Wall_Sec_North", center + new Vector3(0, 1.75f, 7.5f), new Vector3(15, 3.5f, 0.4f), matConcrete);
        // South Wall
        CreateBox(secObj.transform, "Wall_Sec_South", center + new Vector3(0, 1.75f, -7.5f), new Vector3(15, 3.5f, 0.4f), matConcrete);
        // East Wall (outer)
        CreateBox(secObj.transform, "Wall_Sec_East", center + new Vector3(7.5f, 1.75f, 0), new Vector3(0.4f, 3.5f, 15), matConcrete);

        // Heavy Steel Bulkhead Door frame with magnetic lock
        CreateBox(props, "Bulkhead_DoorFrame_Left", new Vector3(10.2f, 1.5f, 1.5f), new Vector3(0.5f, 3f, 0.3f), matMetal);
        CreateBox(props, "Bulkhead_DoorFrame_Right", new Vector3(10.2f, 1.5f, -1.5f), new Vector3(0.5f, 3f, 0.3f), matMetal);
        CreateBox(props, "Bulkhead_DoorFrame_Top", new Vector3(10.2f, 3.1f, 0), new Vector3(0.5f, 0.4f, 3.3f), matMetal);
        // Half-opened jammed heavy blast door
        var blastDoor = CreateBox(props, "Blast_Door_Jammed", new Vector3(10.2f, 1.5f, -0.5f), new Vector3(0.15f, 3f, 2.2f), matMetal);
        blastDoor.transform.rotation = Quaternion.Euler(0, 15, 0);

        // Hero Asset: Surveillance Console with 12 CRT & Flat Monitors
        BuildSurveillanceConsole(props, center + new Vector3(2f, 0, 0));

        // Wall-mounted Gun Locker (forced open & looted)
        BuildGunLocker(props, center + new Vector3(0, 0, 7.2f));

        // Server Racks with blinking LEDs
        BuildServerRacks(props, center + new Vector3(5.5f, 0, -4f));

        // Lighting:
        // 1. Green phosphor monitor glare
        var screenLightObj = new GameObject("Light_Sec_Monitor_Phosphor");
        screenLightObj.transform.SetParent(lights);
        screenLightObj.transform.position = center + new Vector3(2f, 1.8f, 0);
        var sl = screenLightObj.AddComponent<Light>();
        sl.type = LightType.Point;
        sl.range = 6.5f;
        sl.color = new Color(0.1f, 0.95f, 0.65f); // Green phosphor
        sl.intensity = 2.5f;
        sl.shadows = LightShadows.Soft;

        // 2. Rotating Emergency Red Alarm Beacon
        var alarmObj = new GameObject("Light_Sec_Alarm_Beacon");
        alarmObj.transform.SetParent(lights);
        alarmObj.transform.position = center + new Vector3(0f, 3.3f, 0f);

        var beaconMesh = CreateBox(alarmObj.transform, "Beacon_Housing", Vector3.zero, new Vector3(0.35f, 0.45f, 0.35f), matRedEmissive);

        var rotatingHead = new GameObject("RotatingHead");
        rotatingHead.transform.SetParent(alarmObj.transform);
        rotatingHead.transform.localPosition = Vector3.zero;

        var alarmLightObj = new GameObject("AlarmSpot");
        alarmLightObj.transform.SetParent(rotatingHead.transform);
        alarmLightObj.transform.localPosition = Vector3.zero;
        alarmLightObj.transform.localRotation = Quaternion.Euler(45, 0, 0);

        var al = alarmLightObj.AddComponent<Light>();
        al.type = LightType.Spot;
        al.spotAngle = 65f;
        al.range = 12f;
        al.color = new Color(1.0f, 0.05f, 0.05f); // Deep warning red
        al.intensity = 4.0f;
        al.shadows = LightShadows.Soft;

        var alarmComp = alarmObj.AddComponent<EmergencyAlarmLight>();
        alarmComp.rotatingHead = rotatingHead.transform;
        alarmComp.alarmLight = al;
        alarmComp.beaconRenderer = beaconMesh.GetComponent<Renderer>();
        alarmComp.rotationSpeed = 260f;

        // Decals: Hazard stripes around blast door
        CreateDecalProjector(decals, "Decal_Sec_Hazard_Door", new Vector3(10.5f, 0.05f, 0), new Vector3(0, 90, 0), new Vector3(1.2f, 3.5f, 1f), matDecalHazard);
        // Blood splatter on server rack
        CreateDecalProjector(decals, "Decal_Sec_Blood_Floor", center + new Vector3(2.5f, 0.05f, 1.2f), Vector3.zero, new Vector3(2.5f, 2.5f, 1.5f), matDecalBloodSplatter);
    }

    // --- CLINIC (QUARANTINE MEDICAL INFIRMARY - 8x12m, H: 3.5m) ---
    private static void BuildClinic(Transform arch, Transform props, Transform lights, Transform decals)
    {
        var clinicObj = new GameObject("Room_Clinic"); clinicObj.transform.SetParent(arch);
        Vector3 center = new Vector3(-14f, 0, 4f);

        // Floor: 8x12m Dirty hospital tiles
        CreateBox(clinicObj.transform, "Floor_Clinic", center + new Vector3(0, -0.1f, 0), new Vector3(8, 0.2f, 12), matDirtyTiles);

        // Ceiling: 8x12m Concrete
        CreateBox(clinicObj.transform, "Ceiling_Clinic", center + new Vector3(0, 3.6f, 0), new Vector3(8, 0.2f, 12), matConcrete);

        // Perimeter Walls (Dirty ceramic hospital tiles)
        // North Wall
        CreateBox(clinicObj.transform, "Wall_Clinic_North", center + new Vector3(0, 1.75f, 6f), new Vector3(8, 3.5f, 0.4f), matDirtyTiles);
        // South Wall
        CreateBox(clinicObj.transform, "Wall_Clinic_South", center + new Vector3(0, 1.75f, -6f), new Vector3(8, 3.5f, 0.4f), matDirtyTiles);
        // West Wall (Outer)
        CreateBox(clinicObj.transform, "Wall_Clinic_West", center + new Vector3(-4f, 1.75f, 0), new Vector3(0.4f, 3.5f, 12), matDirtyTiles);

        // Hero Asset: Smashed hydraulic examination gurney tipped sideways with leather restraints
        BuildExaminationGurney(props, center + new Vector3(0.5f, 0, 0));

        // Overturned IV drip stands & shattered medicine cabinet
        BuildMedicalCabinetsAndStands(props, center);

        // Hanging soiled privacy curtains
        BuildPrivacyCurtains(props, center + new Vector3(-1.5f, 0, -1f));

        // Lighting:
        // Sickly cyan-green fluorescent strobe light
        var fluoObj = new GameObject("Light_Clinic_Fluorescent_Strobe");
        fluoObj.transform.SetParent(lights);
        fluoObj.transform.position = center + new Vector3(0, 3.2f, 0);
        var fl = fluoObj.AddComponent<Light>();
        fl.type = LightType.Spot;
        fl.spotAngle = 110f;
        fl.range = 8.5f;
        fl.color = new Color(0.45f, 0.95f, 0.85f); // Sickly pale hospital greenish-cyan
        fl.intensity = 1.8f;
        fl.shadows = LightShadows.Soft;
        fluoObj.transform.rotation = Quaternion.Euler(90, 0, 0);

        var flicker = fluoObj.AddComponent<FlickeringLight>();
        flicker.minIntensity = 0.05f;
        flicker.maxIntensity = 2.2f;
        flicker.minFlickerSpeed = 0.02f;
        flicker.maxFlickerSpeed = 0.18f;
        flicker.outageChance = 0.35f;

        // Decals: Massive pool of coagulated blood under the tipped gurney
        CreateDecalProjector(decals, "Decal_Clinic_BloodPool_Gurney", center + new Vector3(0.5f, 0.05f, -0.2f), Vector3.zero, new Vector3(4.2f, 4.2f, 2f), matDecalBloodPool);
        // Blood handprints and splatters against the white tiled wall
        CreateDecalProjector(decals, "Decal_Clinic_BloodWall", center + new Vector3(-3.85f, 1.6f, 0.5f), new Vector3(0, 90, 0), new Vector3(2.5f, 2.5f, 1.5f), matDecalBloodSplatter);
    }

    // --- ENTRANCE (10x10m, H: 5m) ---
    private static void BuildEntrance(Transform arch, Transform props, Transform lights)
    {
        var entObj = new GameObject("Room_Entrance"); entObj.transform.SetParent(arch);
        Vector3 center = new Vector3(0, 0, 15f);

        // Floor: 10x10m Marble
        CreateBox(entObj.transform, "Floor_Entrance", center + new Vector3(0, -0.1f, 0), new Vector3(10, 0.2f, 10), matMarble);
        // Ceiling: 10x10m Concrete
        CreateBox(entObj.transform, "Ceiling_Entrance", center + new Vector3(0, 5.1f, 0), new Vector3(10, 0.2f, 10), matConcrete);

        // North Outer Gate (Reinforced Security Gate)
        CreateBox(entObj.transform, "Wall_Entrance_North", center + new Vector3(0, 2.5f, 5f), new Vector3(10, 5f, 0.5f), matMetal);
        // West Wall
        CreateBox(entObj.transform, "Wall_Entrance_West", center + new Vector3(-5f, 2.5f, 0), new Vector3(0.4f, 5f, 10), matWalnut);
        // East Wall
        CreateBox(entObj.transform, "Wall_Entrance_East", center + new Vector3(5f, 2.5f, 0), new Vector3(0.4f, 5f, 10), matWalnut);

        // Security Metal Detector Gate
        var detLeft = CreateBox(props, "MetalDetector_Left", center + new Vector3(-1.2f, 1.5f, 2f), new Vector3(0.3f, 3f, 0.8f), matMetal);
        var detRight = CreateBox(props, "MetalDetector_Right", center + new Vector3(1.2f, 1.5f, 2f), new Vector3(0.3f, 3f, 0.8f), matMetal);
        var detTop = CreateBox(props, "MetalDetector_Top", center + new Vector3(0, 3.1f, 2f), new Vector3(2.7f, 0.25f, 0.8f), matMetal);

        // Dim moody lighting
        var lObj = new GameObject("Light_Entrance");
        lObj.transform.SetParent(lights);
        lObj.transform.position = center + new Vector3(0, 4.2f, 0);
        var l = lObj.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 9f;
        l.color = new Color(0.6f, 0.75f, 0.9f);
        l.intensity = 1.2f;
        l.shadows = LightShadows.Soft;
    }

    // --- CORRIDORS & ELEVATOR (10x30m, H: 4m) ---
    private static void BuildCorridorsAndElevator(Transform arch, Transform props, Transform lights, Transform decals)
    {
        var corrObj = new GameObject("Corridor_South_And_Elevator"); corrObj.transform.SetParent(arch);

        // South Corridor: X: -5 to 5, Z: -10 to -40 (Center: (0, 0, -25), Size: (10, 0.2, 30))
        Vector3 corrCenter = new Vector3(0, 0, -25f);
        CreateBox(corrObj.transform, "Floor_South_Corridor", corrCenter + new Vector3(0, -0.1f, 0), new Vector3(10, 0.2f, 30), matMetal);
        CreateBox(corrObj.transform, "Ceiling_South_Corridor", corrCenter + new Vector3(0, 4.1f, 0), new Vector3(10, 0.2f, 30), matConcrete);
        CreateBox(corrObj.transform, "Wall_Corr_West", corrCenter + new Vector3(-5f, 2f, 0), new Vector3(0.4f, 4f, 30), matConcrete);
        CreateBox(corrObj.transform, "Wall_Corr_East", corrCenter + new Vector3(5f, 2f, 0), new Vector3(0.4f, 4f, 30), matConcrete);

        // Pipeline and conduits running along ceiling
        for (int z = -15; z >= -35; z -= 5)
        {
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = "Ceiling_Pipe";
            pipe.transform.SetParent(corrObj.transform);
            pipe.transform.position = new Vector3(-3.5f, 3.7f, z);
            pipe.transform.localScale = new Vector3(0.3f, 2.5f, 0.3f);
            pipe.transform.rotation = Quaternion.Euler(90, 0, 0);
            pipe.GetComponent<Renderer>().sharedMaterial = matMetal;
        }

        // Elevator Chamber (15x15m) at Z = -47.5f
        Vector3 elCenter = new Vector3(0, 0, -47.5f);
        CreateBox(corrObj.transform, "Floor_Elevator_Hall", elCenter + new Vector3(0, -0.1f, 0), new Vector3(15, 0.2f, 15), matMetal);
        CreateBox(corrObj.transform, "Ceiling_Elevator_Hall", elCenter + new Vector3(0, 4.1f, 0), new Vector3(15, 0.2f, 15), matConcrete);
        CreateBox(corrObj.transform, "Wall_Elevator_South", elCenter + new Vector3(0, 2f, -7.5f), new Vector3(15, 4f, 0.4f), matConcrete);
        CreateBox(corrObj.transform, "Wall_Elevator_West", elCenter + new Vector3(-7.5f, 2f, 0), new Vector3(0.4f, 4f, 15), matConcrete);
        CreateBox(corrObj.transform, "Wall_Elevator_East", elCenter + new Vector3(7.5f, 2f, 0), new Vector3(0.4f, 4f, 15), matConcrete);

        // Central Elevator Shaft Doors (Heavy industrial freight elevator)
        CreateBox(props, "Elevator_Door_Frame", elCenter + new Vector3(0, 2f, -7.2f), new Vector3(4.5f, 3.5f, 0.4f), matMetal);
        var elDoorLeft = CreateBox(props, "Elevator_Door_Left", elCenter + new Vector3(-1.1f, 1.8f, -7.15f), new Vector3(1.8f, 3.2f, 0.15f), matMetal);
        var elDoorRight = CreateBox(props, "Elevator_Door_Right", elCenter + new Vector3(1.1f, 1.8f, -7.15f), new Vector3(1.8f, 3.2f, 0.15f), matMetal);
        // Call button panel with red backlight
        CreateBox(props, "Elevator_Call_Panel", elCenter + new Vector3(2.8f, 1.4f, -7.1f), new Vector3(0.3f, 0.6f, 0.1f), matRedEmissive);

        // Hazard stripes in front of elevator shaft
        CreateDecalProjector(decals, "Decal_Elevator_Hazard", elCenter + new Vector3(0, 0.05f, -5.5f), Vector3.zero, new Vector3(4.5f, 1.5f, 1f), matDecalHazard);

        // Dim rhythmic fluorescent lights down the corridor
        for (int z = -15; z >= -35; z -= 10)
        {
            var clObj = new GameObject("Light_Corridor_" + z);
            clObj.transform.SetParent(lights);
            clObj.transform.position = new Vector3(0, 3.6f, z);
            var cl = clObj.AddComponent<Light>();
            cl.type = LightType.Point;
            cl.range = 8f;
            cl.color = new Color(0.7f, 0.85f, 1.0f);
            cl.intensity = 1.0f;
            cl.shadows = LightShadows.Soft;
        }

        // Elevator red warning light
        var elLightObj = new GameObject("Light_Elevator_Beacon");
        elLightObj.transform.SetParent(lights);
        elLightObj.transform.position = elCenter + new Vector3(0, 3.6f, -6.5f);
        var elL = elLightObj.AddComponent<Light>();
        elL.type = LightType.Spot;
        elL.spotAngle = 90f;
        elL.range = 8f;
        elL.color = new Color(1f, 0.15f, 0.15f);
        elL.intensity = 2.5f;
        elL.shadows = LightShadows.Soft;
        elLightObj.transform.rotation = Quaternion.Euler(90, 0, 0);
    }

    // --- HERO PROP BUILDERS ---

    private static void BuildReceptionDesk(Transform parent, Vector3 pos)
    {
        var deskRoot = new GameObject("Prop_Hero_ReceptionDesk");
        deskRoot.transform.SetParent(parent);
        deskRoot.transform.position = pos;

        // Main Curved Mahogany Counter (segmented box arc)
        CreateBox(deskRoot.transform, "Desk_Segment_Center", new Vector3(0, 0.6f, 0), new Vector3(3.2f, 1.2f, 1.0f), matWalnut);
        
        var leftWing = CreateBox(deskRoot.transform, "Desk_Segment_Left", new Vector3(-2.0f, 0.6f, -0.6f), new Vector3(2.0f, 1.2f, 1.0f), matWalnut);
        leftWing.transform.localRotation = Quaternion.Euler(0, 35, 0);

        var rightWing = CreateBox(deskRoot.transform, "Desk_Segment_Right", new Vector3(2.0f, 0.6f, -0.6f), new Vector3(2.0f, 1.2f, 1.0f), matWalnut);
        rightWing.transform.localRotation = Quaternion.Euler(0, -35, 0);

        // Countertop (recessed bronze & polished wood)
        CreateBox(deskRoot.transform, "Counter_Top", new Vector3(0, 1.22f, 0), new Vector3(3.4f, 0.08f, 1.15f), matWalnut);

        // Shattered glass partition on top of desk
        var glass1 = CreateBox(deskRoot.transform, "Glass_Partition_Intact", new Vector3(-0.9f, 1.7f, 0), new Vector3(1.4f, 0.85f, 0.05f), matGlass);
        var glass2 = CreateBox(deskRoot.transform, "Glass_Partition_Shattered", new Vector3(0.9f, 1.45f, 0), new Vector3(1.4f, 0.45f, 0.05f), matGlass);
        glass2.transform.localRotation = Quaternion.Euler(0, 5, -8); // bent/cracked angle

        // Check-in Tablet (blood smeared on screen)
        var tablet = CreateBox(deskRoot.transform, "Tablet_CheckIn", new Vector3(-0.6f, 1.3f, 0.2f), new Vector3(0.35f, 0.04f, 0.25f), matMetal);
        tablet.transform.localRotation = Quaternion.Euler(20, -15, 0);

        // Holographic illuminated ARXIS corporate logo on front of desk
        var logoObj = CreateBox(deskRoot.transform, "Arxis_HoloLogo", new Vector3(0, 0.6f, 0.52f), new Vector3(1.2f, 0.4f, 0.05f), matCRTEmissive);

        // Scattered documents/dossiers on floor and desk
        for (int i = 0; i < 8; i++)
        {
            float rx = Random.Range(-1.5f, 1.5f);
            float rz = Random.Range(-1.2f, 1.2f);
            var paper = CreateBox(deskRoot.transform, "Dossier_Paper_" + i, new Vector3(rx, 0.02f, rz), new Vector3(0.25f, 0.005f, 0.35f), matConcrete);
            paper.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        }
    }

    private static void BuildWaitingArea(Transform parent, Vector3 pos)
    {
        var waitRoot = new GameObject("Prop_Lobby_WaitingArea");
        waitRoot.transform.SetParent(parent);
        waitRoot.transform.position = pos;

        // Coffee table (glass and metal)
        CreateBox(waitRoot.transform, "CoffeeTable_Glass", new Vector3(0, 0.45f, 0), new Vector3(2.2f, 0.06f, 1.2f), matGlass);
        CreateBox(waitRoot.transform, "CoffeeTable_Frame", new Vector3(0, 0.22f, 0), new Vector3(2.1f, 0.4f, 1.1f), matMetal);

        // Designer Armchairs (one upright, one overturned in panic)
        // Upright chair
        var chair1 = new GameObject("Armchair_Upright"); chair1.transform.SetParent(waitRoot.transform);
        chair1.transform.localPosition = new Vector3(-1.6f, 0, 0);
        CreateBox(chair1.transform, "Seat", new Vector3(0, 0.4f, 0), new Vector3(0.85f, 0.25f, 0.85f), matWalnut);
        CreateBox(chair1.transform, "Back", new Vector3(-0.35f, 0.8f, 0), new Vector3(0.2f, 0.7f, 0.85f), matWalnut);

        // Overturned chair (Resident Evil panic scene)
        var chair2 = new GameObject("Armchair_Overturned"); chair2.transform.SetParent(waitRoot.transform);
        chair2.transform.localPosition = new Vector3(1.6f, 0.35f, 0);
        chair2.transform.localRotation = Quaternion.Euler(75, 40, -15);
        CreateBox(chair2.transform, "Seat", new Vector3(0, 0.4f, 0), new Vector3(0.85f, 0.25f, 0.85f), matWalnut);
        CreateBox(chair2.transform, "Back", new Vector3(-0.35f, 0.8f, 0), new Vector3(0.2f, 0.7f, 0.85f), matWalnut);
    }

    private static void BuildBrokenGlassDoors(Transform parent, Vector3 pos)
    {
        var doorsRoot = new GameObject("Prop_BrokenGlassDoors");
        doorsRoot.transform.SetParent(parent);
        doorsRoot.transform.position = pos;

        // Frame
        CreateBox(doorsRoot.transform, "Frame_Left", new Vector3(-2.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 0.2f), matMetal);
        CreateBox(doorsRoot.transform, "Frame_Right", new Vector3(2.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 0.2f), matMetal);
        CreateBox(doorsRoot.transform, "Frame_Top", new Vector3(0, 3.6f, 0), new Vector3(4.4f, 0.2f, 0.2f), matMetal);

        // Left door pane (hanging loosely on bent bottom hinge)
        var doorL = CreateBox(doorsRoot.transform, "Door_Left_Bent", new Vector3(-1.0f, 1.6f, 0.4f), new Vector3(1.8f, 3.2f, 0.06f), matGlass);
        doorL.transform.localRotation = Quaternion.Euler(8, 25, -4);

        // Right door pane (completely shattered, frame hanging)
        var doorR = CreateBox(doorsRoot.transform, "Door_Right_BrokenFrame", new Vector3(1.0f, 1.7f, -0.3f), new Vector3(1.8f, 3.2f, 0.06f), matMetal);
        doorR.transform.localRotation = Quaternion.Euler(0, -65, 0);
    }

    private static void BuildFallenCeilingDebris(Transform parent, Vector3 pos)
    {
        var debRoot = new GameObject("Prop_CeilingDebris");
        debRoot.transform.SetParent(parent);
        debRoot.transform.position = pos;

        // Smashed acoustic tiles on floor
        for (int i = 0; i < 6; i++)
        {
            var tile = CreateBox(debRoot.transform, "Fallen_Tile_" + i, new Vector3(Random.Range(-1.5f, 1.5f), 0.02f * (i + 1), Random.Range(-1.5f, 1.5f)), new Vector3(1.2f, 0.04f, 0.8f), matConcrete);
            tile.transform.localRotation = Quaternion.Euler(Random.Range(-5, 5), Random.Range(0, 360), Random.Range(-5, 5));
        }

        // Exposed hanging metal conduit
        var conduit = CreateBox(debRoot.transform, "Hanging_Conduit_Tray", new Vector3(0, 3.2f, 0), new Vector3(0.4f, 2.5f, 0.15f), matMetal);
        conduit.transform.localRotation = Quaternion.Euler(0, 0, 18);
    }

    private static void BuildSurveillanceConsole(Transform parent, Vector3 pos)
    {
        var consoleRoot = new GameObject("Prop_Hero_SurveillanceConsole");
        consoleRoot.transform.SetParent(parent);
        consoleRoot.transform.position = pos;

        // Curved Console Desk
        CreateBox(consoleRoot.transform, "Console_Desk_Base", new Vector3(0, 0.5f, 0), new Vector3(4.5f, 1.0f, 1.2f), matMetal);
        CreateBox(consoleRoot.transform, "Console_Desk_Top", new Vector3(0, 1.02f, 0), new Vector3(4.7f, 0.08f, 1.3f), matMetal);

        // Bank of 12 CCTV and CRT Monitors arranged in 2 rows of 6
        for (int col = 0; col < 6; col++)
        {
            float xOffset = (col - 2.5f) * 0.72f;
            float curveZ = Mathf.Abs(col - 2.5f) * 0.1f; // Slight ergonomic curve

            // Bottom row monitor
            var monBottom = CreateBox(consoleRoot.transform, "Monitor_B_" + col, new Vector3(xOffset, 1.45f, curveZ), new Vector3(0.65f, 0.45f, 0.15f), matCRTEmissive);
            monBottom.transform.localRotation = Quaternion.Euler(-5, -(col - 2.5f) * 4f, 0);

            // Top row monitor
            var monTop = CreateBox(consoleRoot.transform, "Monitor_T_" + col, new Vector3(xOffset, 2.05f, curveZ + 0.05f), new Vector3(0.65f, 0.45f, 0.15f), matCRTEmissive);
            monTop.transform.localRotation = Quaternion.Euler(10, -(col - 2.5f) * 4f, 0);
        }

        // Swivel Office Chair (tipped over)
        var chair = CreateBox(consoleRoot.transform, "SwivelChair_Tipped", new Vector3(0, 0.35f, 1.2f), new Vector3(0.7f, 0.7f, 0.7f), matWalnut);
        chair.transform.localRotation = Quaternion.Euler(60, 20, 10);
    }

    private static void BuildGunLocker(Transform parent, Vector3 pos)
    {
        var lockerRoot = new GameObject("Prop_GunLocker");
        lockerRoot.transform.SetParent(parent);
        lockerRoot.transform.position = pos;

        // Locker frame (heavy tactical steel)
        CreateBox(lockerRoot.transform, "Locker_Cabinet", new Vector3(0, 1.5f, 0), new Vector3(2.5f, 3.0f, 0.8f), matMetal);

        // Forced open mesh doors
        var doorL = CreateBox(lockerRoot.transform, "Locker_Door_Left", new Vector3(-1.3f, 1.5f, 0.45f), new Vector3(1.2f, 2.8f, 0.08f), matMetal);
        doorL.transform.localRotation = Quaternion.Euler(0, -95, 0);

        var doorR = CreateBox(lockerRoot.transform, "Locker_Door_Right", new Vector3(1.3f, 1.5f, 0.45f), new Vector3(1.2f, 2.8f, 0.08f), matMetal);
        doorR.transform.localRotation = Quaternion.Euler(0, 110, 0);

        // Empty rifle rack pegs inside
        for (int i = 0; i < 5; i++)
        {
            CreateBox(lockerRoot.transform, "RackSlot_" + i, new Vector3((i - 2) * 0.4f, 1.6f, -0.1f), new Vector3(0.1f, 0.8f, 0.2f), matMetal);
        }
    }

    private static void BuildServerRacks(Transform parent, Vector3 pos)
    {
        var rackRoot = new GameObject("Prop_ServerRacks");
        rackRoot.transform.SetParent(parent);
        rackRoot.transform.position = pos;

        for (int i = 0; i < 3; i++)
        {
            var rack = CreateBox(rackRoot.transform, "ServerChassis_" + i, new Vector3(i * 1.1f, 1.5f, 0), new Vector3(0.9f, 3.0f, 1.1f), matMetal);
            // Blinking LED indicator strips on front face
            CreateBox(rack.transform, "LED_Strip", new Vector3(0, 0, 0.56f), new Vector3(0.6f, 2.4f, 0.02f), matCRTEmissive);
        }
    }

    private static void BuildExaminationGurney(Transform parent, Vector3 pos)
    {
        var gurneyRoot = new GameObject("Prop_Hero_ExaminationGurney");
        gurneyRoot.transform.SetParent(parent);
        gurneyRoot.transform.position = pos;
        gurneyRoot.transform.rotation = Quaternion.Euler(45, 25, -15); // Smashed & tipped sideways

        // Stainless steel stretcher frame
        CreateBox(gurneyRoot.transform, "Gurney_Frame", new Vector3(0, 0.45f, 0), new Vector3(1.0f, 0.15f, 2.4f), matMetal);
        // Blood-stained mattress with torn leather restraints
        CreateBox(gurneyRoot.transform, "Gurney_Mattress", new Vector3(0, 0.6f, 0), new Vector3(0.9f, 0.15f, 2.3f), matWalnut);
        // Hydraulic base and wheels
        CreateBox(gurneyRoot.transform, "Gurney_Hydraulics", new Vector3(0, 0.2f, 0), new Vector3(0.6f, 0.35f, 1.6f), matMetal);
    }

    private static void BuildMedicalCabinetsAndStands(Transform parent, Vector3 center)
    {
        var medRoot = new GameObject("Prop_MedicalSupplies");
        medRoot.transform.SetParent(parent);
        medRoot.transform.position = center;

        // Overturned IV Drip Stand on floor
        var ivStand = new GameObject("IV_DripStand_Fallen");
        ivStand.transform.SetParent(medRoot.transform);
        ivStand.transform.localPosition = new Vector3(-2.2f, 0.15f, 2.0f);
        ivStand.transform.localRotation = Quaternion.Euler(85, -30, 0);
        CreateBox(ivStand.transform, "Pole", Vector3.zero, new Vector3(0.08f, 2.0f, 0.08f), matMetal);
        CreateBox(ivStand.transform, "Base", new Vector3(0, -1.0f, 0), new Vector3(0.6f, 0.1f, 0.6f), matMetal);

        // Shattered Medicine Cabinet on wall
        var cabinet = CreateBox(medRoot.transform, "Medicine_Cabinet", new Vector3(-3.8f, 1.8f, -2.5f), new Vector3(0.35f, 1.4f, 1.8f), matMetal);
        CreateBox(cabinet.transform, "Cabinet_ShatteredGlass", new Vector3(0.18f, 0, 0), new Vector3(0.02f, 1.3f, 1.7f), matGlass);
    }

    private static void BuildPrivacyCurtains(Transform parent, Vector3 pos)
    {
        var curtainRoot = new GameObject("Prop_PrivacyCurtains");
        curtainRoot.transform.SetParent(parent);
        curtainRoot.transform.position = pos;

        // Curved ceiling track
        CreateBox(curtainRoot.transform, "Curtain_Track", new Vector3(0, 3.2f, 0), new Vector3(2.5f, 0.08f, 0.08f), matMetal);
        // Hanging stained fabric curtain
        var curtain = CreateBox(curtainRoot.transform, "Curtain_Fabric", new Vector3(0, 1.6f, 0), new Vector3(2.2f, 3.0f, 0.05f), matDirtyTiles);
        curtain.transform.localRotation = Quaternion.Euler(0, 15, -3); // draped loosely
    }

    // --- UTILITIES ---

    private static GameObject CreateBox(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    private static void CreateColumn(Transform parent, Vector3 pos, float height, float width, Material matCore, Material matTrim)
    {
        var colRoot = new GameObject("Pillar");
        colRoot.transform.SetParent(parent);
        colRoot.transform.localPosition = pos;

        // Core
        CreateBox(colRoot.transform, "Core", Vector3.zero, new Vector3(width, height, width), matCore);
        // Base collar
        CreateBox(colRoot.transform, "Base_Collar", new Vector3(0, -height * 0.5f + 0.2f, 0), new Vector3(width * 1.3f, 0.4f, width * 1.3f), matTrim);
        // Top capital collar
        CreateBox(colRoot.transform, "Top_Collar", new Vector3(0, height * 0.5f - 0.2f, 0), new Vector3(width * 1.3f, 0.4f, width * 1.3f), matTrim);
    }

    private static void CreateDecalProjector(Transform parent, string name, Vector3 pos, Vector3 rot, Vector3 size, Material mat)
    {
        var decalGo = new GameObject(name);
        decalGo.transform.SetParent(parent);
        decalGo.transform.position = pos;
        decalGo.transform.rotation = Quaternion.Euler(rot.x + 90f, rot.y, rot.z); // Face downward onto floor/wall

        var proj = decalGo.AddComponent<DecalProjector>();
        proj.size = size;
        if (mat != null) proj.material = mat;
    }

    private static void SetupEnvironmentLighting(Transform lights)
    {
        // Directional Moonlight
        var dirObj = new GameObject("Directional_Moonlight");
        dirObj.transform.SetParent(lights);
        var dirLight = dirObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(0.25f, 0.45f, 0.75f); // Cold moonlight
        dirLight.intensity = 0.35f; // Moody dark survival horror key light
        dirLight.shadows = LightShadows.Soft;
        dirObj.transform.rotation = Quaternion.Euler(50f, -40f, 0f);

        // Global Ambient Settings
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.02f, 0.03f, 0.05f); // Deep pitch black shadows

        // Global Volume for Survival Horror Post-Processing
        var volObj = new GameObject("Global_Horror_Volume");
        volObj.transform.SetParent(lights);
        var vol = volObj.AddComponent<Volume>();
        vol.isGlobal = true;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Level1_Horror_VolumeProfile";

        // 1. Tonemapping (ACES)
        var tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        // 2. Color Adjustments
        var colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.Override(-0.15f);
        colorAdj.contrast.Override(35f);
        colorAdj.saturation.Override(-15f);

        // 3. Vignette
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.38f);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(Color.black);

        // 4. Film Grain
        var filmGrain = profile.Add<FilmGrain>(true);
        filmGrain.type.Override(FilmGrainLookup.Medium1);
        filmGrain.intensity.Override(0.28f);
        filmGrain.response.Override(0.8f);

        // 5. Bloom
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.7f);
        bloom.threshold.Override(1.1f);
        bloom.scatter.Override(0.75f);

        vol.profile = profile;

        AssetDatabase.CreateAsset(profile, "Assets/_Project/Environments/Level1_Admin/Level1_Horror_VolumeProfile.asset");
    }

    private static void SetupCamera(Transform root)
    {
        var camObj = new GameObject("Cinematic_MainCamera");
        camObj.transform.SetParent(root);
        camObj.transform.position = new Vector3(0f, 2.2f, -7.0f);
        camObj.transform.rotation = Quaternion.Euler(10f, 0f, 0f); // High dramatic third-person RE angle overlooking reception desk

        var cam = camObj.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.fieldOfView = 65f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 150f;

        camObj.AddComponent<AudioListener>();

        // Enable post-processing on camera
        var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;
    }
}
#endif
