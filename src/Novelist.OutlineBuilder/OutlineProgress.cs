namespace Novelist.OutlineBuilder
{
    /// <summary>
    /// Ordered list of pipeline phases in the outline‑builder pipeline.
    /// </summary>
    public enum OutlineProgress
    {
        Init,
        PremiseExpanded,
        ArcDefined,
        CharactersOutlined,
        SubPlotsDefined,
        BeatsExpanded,
        StructureOutlined,
        BeatsDetailed,
        Finalized
    }
}
