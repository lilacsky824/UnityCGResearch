using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

[CustomEditor(typeof(SpineVATBaker))]
public class SpineVATBakerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        // Create a new VisualElement root container
        VisualElement root = new VisualElement();

        // Create the default inspector
        InspectorElement.FillDefaultInspector(root, serializedObject, this);

        // Create a button for VAT generation
        Button generateVATButton = new Button(() =>
        {
            // Get the target object and cast it to SpineVATBaker
            SpineVATBaker baker = (SpineVATBaker)target;
            baker.GenerateVAT();
        })
        {
            text = "Generate VAT"
        };

        // Add the button to the root element
        root.Add(generateVATButton);

        return root;
    }
}
