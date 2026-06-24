#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BucketFluidConnector))]
public class BucketFluidConnectorEditor : Editor
{
    private BucketFluidConnector connector;
    private bool showDebugInfo = false;

    private void OnEnable()
    {
        connector = (BucketFluidConnector)target;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Bucket Fluid Connector", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Connects bucket physics/view/mass to fluid simulation systems (SPH, paint drops, etc.)", MessageType.Info);
        EditorGUILayout.Space(5);

        // Auto-find toggle
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoFindComponents"));
        EditorGUILayout.Space(5);

        // Component references
        EditorGUILayout.LabelField("Component References", EditorStyles.boldLabel);
        
        SerializedProperty physicsProperty = serializedObject.FindProperty("physics");
        SerializedProperty viewProperty = serializedObject.FindProperty("view");
        SerializedProperty massProperty = serializedObject.FindProperty("mass");

        DrawComponentField("Physics", physicsProperty, connector.Physics != null);
        DrawComponentField("View", viewProperty, connector.View != null);
        DrawComponentField("Mass", massProperty, connector.Mass != null);

        EditorGUILayout.Space(10);

        // Auto-wire button
        if (GUILayout.Button("🔌 Auto-Wire Components", GUILayout.Height(30)))
        {
            AutoWireComponents();
        }

        EditorGUILayout.Space(5);

        // Validation
        DrawValidation();

        EditorGUILayout.Space(10);

        // Debug info
        showDebugInfo = EditorGUILayout.Foldout(showDebugInfo, "Debug Info (Runtime)", true);
        if (showDebugInfo && Application.isPlaying)
        {
            DrawDebugInfo();
        }
        else if (showDebugInfo && !Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Debug info available during Play Mode", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(target);
        }
    }

    private void DrawComponentField(string label, SerializedProperty property, bool isValid)
    {
        EditorGUILayout.BeginHorizontal();
        
        Color oldColor = GUI.color;
        if (!isValid)
        {
            GUI.color = new Color(1f, 0.5f, 0.5f);
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label));
        
        GUI.color = oldColor;

        if (isValid)
        {
            GUILayout.Label("✓", GUILayout.Width(20));
        }
        else
        {
            GUI.color = Color.red;
            GUILayout.Label("✗", GUILayout.Width(20));
            GUI.color = oldColor;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void AutoWireComponents()
    {
        Undo.RecordObject(connector, "Auto-Wire Bucket Components");

        SerializedProperty physicsProperty = serializedObject.FindProperty("physics");
        SerializedProperty viewProperty = serializedObject.FindProperty("view");
        SerializedProperty massProperty = serializedObject.FindProperty("mass");

        BucketPhysics physics = connector.GetComponent<BucketPhysics>();
        BucketView view = connector.GetComponent<BucketView>();
        BucketMass mass = connector.GetComponent<BucketMass>();

        int foundCount = 0;

        if (physics != null)
        {
            physicsProperty.objectReferenceValue = physics;
            foundCount++;
        }

        if (view != null)
        {
            viewProperty.objectReferenceValue = view;
            foundCount++;
        }

        if (mass != null)
        {
            massProperty.objectReferenceValue = mass;
            foundCount++;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);

        if (foundCount == 3)
        {
            Debug.Log($"✅ Successfully auto-wired all 3 components for {connector.gameObject.name}");
        }
        else if (foundCount > 0)
        {
            Debug.LogWarning($"⚠️ Auto-wired {foundCount}/3 components for {connector.gameObject.name}. Some components are missing.");
        }
        else
        {
            Debug.LogError($"❌ No bucket components found on {connector.gameObject.name}. Add BucketPhysics, BucketView, and BucketMass first.");
        }
    }

    private void DrawValidation()
    {
        bool allValid = connector.Physics != null && connector.View != null && connector.Mass != null;

        if (allValid)
        {
            EditorGUILayout.HelpBox("✓ All components connected properly!", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("⚠ Missing components! Click 'Auto-Wire Components' or assign manually.", MessageType.Warning);

            if (connector.Physics == null)
                EditorGUILayout.HelpBox("• Missing BucketPhysics", MessageType.Error);
            if (connector.View == null)
                EditorGUILayout.HelpBox("• Missing BucketView", MessageType.Error);
            if (connector.Mass == null)
                EditorGUILayout.HelpBox("• Missing BucketMass", MessageType.Error);
        }
    }

    private void DrawDebugInfo()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Container State", EditorStyles.boldLabel);
        
        if (connector.Physics != null && connector.View != null)
        {
            Vector3 center = connector.GetContainerCenter();
            Vector3 up = connector.GetContainerUp();
            Vector3 velocity = connector.GetContainerVelocity();
            Vector3 acceleration = connector.GetContainerAcceleration();

            EditorGUILayout.LabelField("Center", center.ToString("F3"));
            EditorGUILayout.LabelField("Up Direction", up.ToString("F3"));
            EditorGUILayout.LabelField("Velocity", $"{velocity.ToString("F2")} ({velocity.magnitude:F2} m/s)");
            EditorGUILayout.LabelField("Acceleration", $"{acceleration.ToString("F2")} ({acceleration.magnitude:F2} m/s²)");
            
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Dimensions", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Top Radius", $"{connector.GetTopRadius():F3} m");
            EditorGUILayout.LabelField("Bottom Radius", $"{connector.GetBottomRadius():F3} m");
            EditorGUILayout.LabelField("Height", $"{connector.GetHeight():F3} m");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Motion State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Current Speed", $"{connector.CurrentSpeed:F2} m/s");
            EditorGUILayout.LabelField("Angular Speed", $"{connector.CurrentAngularSpeed:F2} rad/s");
            EditorGUILayout.LabelField("Is Moving", connector.IsMoving ? "Yes" : "No");

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mass State", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Initial Mass", $"{connector.InitialMass:F2} kg");
            EditorGUILayout.LabelField("Current Mass", $"{connector.CurrentMass:F2} kg");
            
            float ratio = connector.MassRatio;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mass Ratio", $"{ratio:P0}");
            Rect rect = GUILayoutUtility.GetRect(100, 18);
            EditorGUI.ProgressBar(rect, ratio, $"{ratio:P0}");
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.HelpBox("Components not initialized yet", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();

        // Force repaint during play mode
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    // Scene view gizmos
    private void OnSceneGUI()
    {
        if (connector == null || connector.Physics == null || connector.View == null)
            return;

        // Draw container bounds
        Vector3 center = Application.isPlaying ? connector.GetContainerCenter() : GetEditorContainerCenter();
        Vector3 up = Application.isPlaying ? connector.GetContainerUp() : GetEditorContainerUp();
        float topRadius = Application.isPlaying ? connector.GetTopRadius() : connector.View.topWidth * 0.5f;
        float bottomRadius = Application.isPlaying ? connector.GetBottomRadius() : connector.View.bottomWidth * 0.5f;
        float height = Application.isPlaying ? connector.GetHeight() : connector.View.bucketHeight;

        // Draw frustum wireframe
        Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        DrawFrustumWireframe(center, up, bottomRadius, topRadius, height);

        // Draw velocity vector during play mode
        if (Application.isPlaying && connector.IsMoving)
        {
            Vector3 velocity = connector.GetContainerVelocity();
            Handles.color = Color.green;
            Handles.DrawLine(center, center + velocity * 0.5f);
            Handles.Label(center + velocity * 0.5f, $"Velocity: {velocity.magnitude:F2} m/s");

            Vector3 acceleration = connector.GetContainerAcceleration();
            if (acceleration.magnitude > 0.1f)
            {
                Handles.color = Color.red;
                Handles.DrawLine(center, center + acceleration * 0.2f);
                Handles.Label(center + acceleration * 0.2f, $"Accel: {acceleration.magnitude:F2} m/s²");
            }
        }

        // Draw center point
        Handles.color = Color.cyan;
        Handles.SphereHandleCap(0, center, Quaternion.identity, 0.05f, EventType.Repaint);
        Handles.Label(center + Vector3.up * 0.1f, "Container Center");
    }

    private Vector3 GetEditorContainerCenter()
    {
        if (connector.Physics == null || connector.View == null) return Vector3.zero;
        connector.Physics.SyncPreview();
        float handleHeight = 0.28f;
        float dist = handleHeight + connector.View.bucketHeight;
        return connector.Physics.EndPosition + connector.Physics.RopeDirection * dist;
    }

    private Vector3 GetEditorContainerUp()
    {
        if (connector.Physics == null) return Vector3.up;
        connector.Physics.SyncPreview();
        return -connector.Physics.RopeDirection;
    }

    private void DrawFrustumWireframe(Vector3 baseCenter, Vector3 axis, float bottomRadius, float topRadius, float height, int segments = 24)
    {
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, axis);
        Vector3 topCenter = baseCenter + axis * height;

        // Draw bottom circle
        Vector3 prevBase = baseCenter + rot * new Vector3(bottomRadius, 0, 0);
        Vector3 prevTop = topCenter + rot * new Vector3(topRadius, 0, 0);

        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 baseOffset = rot * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * bottomRadius;
            Vector3 topOffset = rot * new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * topRadius;
            Vector3 curBase = baseCenter + baseOffset;
            Vector3 curTop = topCenter + topOffset;

            // Draw circles
            Handles.DrawLine(prevBase, curBase);
            Handles.DrawLine(prevTop, curTop);

            // Draw vertical lines every 6 segments
            if (i % 6 == 0)
            {
                Handles.DrawLine(curBase, curTop);
            }

            prevBase = curBase;
            prevTop = curTop;
        }
    }
}
#endif
