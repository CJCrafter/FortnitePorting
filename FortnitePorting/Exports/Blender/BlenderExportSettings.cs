﻿using FortnitePorting.AppUtils;

namespace FortnitePorting.Exports.Blender;

public class BlenderExportSettings : ExportSettingsBase
{
    // GENERAL
    public bool IntoCollection = true;

    // RIGGING
    public ERigType RigType;
    public bool MergeSkeletons = true;
    public bool ReorientBones;
    public bool HideFaceBones;
    public float BoneLengthRatio = 0.4f;

    // ANIM
    public bool UpdateTimeline;
    public bool LoopAnim;

    // MESH
    public bool QuadTopo;
    public bool PoseFixes;
}