using UnityEngine;

/// <summary>
/// Learning aid: colours the bar and the hands while Enter would catch the bar: green for a clean catch,
/// amber for a catch that gets deductions. Presentation only.
/// It uses a MaterialPropertyBlock, which overrides a colour for one renderer without creating a new
/// material, so the shared materials (and every other object using them) stay unchanged.
/// </summary>
public class CatchIndicator : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");   // URP Lit colour

    [SerializeField] private GymnastController controller;
    [SerializeField] private Renderer[] highlighted;     // the bar and both hands
    [SerializeField] private Color cleanColor = new Color(0.2f, 0.95f, 0.3f);
    [SerializeField] private Color deductionColor = new Color(1f, 0.8f, 0.15f);

    private MaterialPropertyBlock block;
    private CatchPreview? shown;    // null until the first Update

    private void Awake()
    {
        block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        CatchPreview preview = controller.CatchPreview;
        if (preview == shown) return;   // only touch the renderers when the state changes
        shown = preview;

        foreach (var target in highlighted)
        {
            if (preview == CatchPreview.None)
            {
                target.SetPropertyBlock(null);  // back to the material's own colour
                continue;
            }
            block.SetColor(BaseColor, preview == CatchPreview.Clean ? cleanColor : deductionColor);
            target.SetPropertyBlock(block);
        }
    }
}
