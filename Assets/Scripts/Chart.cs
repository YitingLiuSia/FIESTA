﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using IATK;
using VRTK;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Lifetime;
using DG.Tweening;
using ExitGames.Client.Photon;
using VRTK.Examples;

/// <summary>
/// Acts as a wrapper for IATK's visualisation script
/// </summary>
public class Chart : Photon.MonoBehaviour
{
    [SerializeField]
    private GameObject visualisationGameObject;
    [SerializeField]
    private Visualisation visualisation;
    [SerializeField]
    private BoxCollider boxCollider;
    [SerializeField]
    private BoxCollider raycastCollider;
    [SerializeField]
    private Rigidbody rigidbody;
    [SerializeField]
    private VRTK_InteractableObject interactableObject;
    [SerializeField]
    private VRTK_InteractableObject topLeftInteractableHandle;
    [SerializeField]
    private VRTK_InteractableObject bottomRightInteractableHandle;

    private Chart[,] splomCharts;  // Stored as 2D array
    private List<Chart> subCharts;  // Stored as 1D array
    private SPLOMButton[] splomButtons;
    private List<GameObject> facetLabels;
    private Key facetSplomKey;

    private Vector3 previousPosition;
    private bool isThrowing = false;
    private bool isTouchingDisplayScreen = false;
    private DisplayScreen touchingDisplayScreen;
    private bool isResizing = false;
    private bool isTouchingDashboard = false;
    private Dashboard touchingDashboard;

    private Vector3 originalWorldPos;
    private Vector3 originalPos;
    private Quaternion originalRot;

    private float deletionTimer = 0;

    #region VisualisationProperties

    public Visualisation Visualisation
    {
        get { return visualisation; }
    }

    public DataSource DataSource
    {
        get { return visualisation.dataSource; }
        set { visualisation.dataSource = value; }
    }

    /// <summary>
    /// ChartType is the type of the chart (the wrapper to the visualisation)
    /// </summary>
    private AbstractVisualisation.VisualisationTypes chartType;
    public AbstractVisualisation.VisualisationTypes VisualisationType
    {
        get { return chartType; }
        set
        {
            if (value == chartType)
                return;

            photonView.RPC("PropagateVisualisationType", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateVisualisationType(AbstractVisualisation.VisualisationTypes value)
    {
        chartType = value;

        switch (value)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                visualisation.visualisationType = value;
                visualisation.CreateVisualisation(value);
                SetAsScatterplot();
                break;

            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
                SetAsScatterplotMatrix();
                break;

            case AbstractVisualisation.VisualisationTypes.FACET:
                SetAsFacet();
                break;
        }
    }

    public AbstractVisualisation.GeometryType GeometryType
    {
        get { return visualisation.geometry; }
        set
        {
            if (value == GeometryType)
                return;

            photonView.RPC("PropagateGeometryType", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateGeometryType(AbstractVisualisation.GeometryType value)
    {
        visualisation.geometry = value;
        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.GeometryType);

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                    chart.GeometryType = value;
                break;
        }
    }

    public string XDimension
    {
        get { return visualisation.xDimension.Attribute; }
        set
        {
            if (value == XDimension)
                return;

            photonView.RPC("PropagateXDimension", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateXDimension(string value)
    {
        visualisation.xDimension = value;

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                visualisation.updateViewProperties(AbstractVisualisation.PropertyType.X);
                CenterVisualisation();
                SetColliderBounds();
                break;

            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.XDimension = value;
                }
                break;
        }
    }

    public string YDimension
    {
        get { return visualisation.yDimension.Attribute; }
        set
        {
            if (value == YDimension)
                return;

            photonView.RPC("PropagateYDimension", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateYDimension(string value)
    {
        visualisation.yDimension = value;

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Y);
                CenterVisualisation();
                SetColliderBounds();
                break;

            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.YDimension = value;
                }
                break;
        }
    }

    public string ZDimension
    {
        get { return visualisation.zDimension.Attribute; }
        set
        {
            if (value == ZDimension)
                return;

            photonView.RPC("PropagateZDimension", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateZDimension(string value)
    {
        visualisation.zDimension = value;

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Z);
                CenterVisualisation();
                SetColliderBounds();
                break;

            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.ZDimension = value;
                }
                break;
        }
    }

    public Vector2 XNormaliser
    {
        get { return new Vector2(visualisation.xDimension.minScale, visualisation.xDimension.maxScale); }
        set
        {
            if (value == XNormaliser)
                return;

            photonView.RPC("PropagateXNormaliser", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateXNormaliser(Vector2 value)
    {
        visualisation.xDimension.minScale = value.x;
        visualisation.xDimension.maxScale = value.y;
        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Scaling);

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.XNormaliser = value;
                }
                break;
        }
    }

    public Vector2 YNormaliser
    {
        get { return new Vector2(visualisation.yDimension.minScale, visualisation.yDimension.maxScale); }
        set
        {
            if (value == YNormaliser)
                return;

            photonView.RPC("PropagateYNormaliser", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateYNormaliser(Vector2 value)
    {

        visualisation.yDimension.minScale = value.x;
        visualisation.yDimension.maxScale = value.y;
        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Scaling);

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.YNormaliser = value;
                }
                break;
        }
    }

    public Vector2 ZNormaliser
    {
        get { return new Vector2(visualisation.zDimension.minScale, visualisation.zDimension.maxScale); }
        set
        {
            if (value == XNormaliser)
                return;

            photonView.RPC("PropagateZNormaliser", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateZNormaliser(Vector2 value)
    {
        visualisation.zDimension.minScale = value.x;
        visualisation.zDimension.maxScale = value.y;
        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Scaling);

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.ZNormaliser = value;
                }
                break;
        }
    }

    public string ColorDimension
    {
        get { return visualisation.colourDimension; }
        set
        {
            if (value == ColorDimension)
                return;

            photonView.RPC("PropagateColorDimension", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateColorDimension(string value)
    {
        visualisation.colourDimension = value;
        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Colour);

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.ColorDimension = value;
                }
                facetSplomKey.UpdateProperties(AbstractVisualisation.PropertyType.None, visualisation);
                break;
        }
    }

    public string ColorPaletteDimension
    {
        get { return visualisation.colorPaletteDimension; }
        set
        {
            if (value == ColorPaletteDimension)
                return;

            photonView.RPC("PropagateColorPaletteDimension", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateColorPaletteDimension(string value)
    {
        visualisation.colorPaletteDimension = value;

        // Make sure the color palette is populated
        if (value != "Undefined")
            visualisation.coloursPalette = new Color[DataSource[value].Data.Distinct().Count()];

        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Colour);

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.ColorPaletteDimension = value;
                }
                facetSplomKey.UpdateProperties(AbstractVisualisation.PropertyType.None, visualisation);
                break;
        }
    }

    public Color Color
    {
        get { return visualisation.colour; }
        set
        {
            if (value == Color)
                return;

            photonView.RPC("PropagateColor", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateColor(Color value)
    {
        visualisation.colour = value;
        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Colour);

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                    chart.Color = value;
                break;
        }
    }

    public Gradient Gradient
    {
        get { return visualisation.dimensionColour; }
        set
        {
            if (value == Gradient)
                return;

            visualisation.dimensionColour = value;
            
            // Update all instances of this chart in other clients, convert to array for serialization
            List<float> gradientList = new List<float>();
            foreach (GradientColorKey colorKey in value.colorKeys)
            {
                gradientList.Add(colorKey.time);
                gradientList.Add(colorKey.color.r);
                gradientList.Add(colorKey.color.g);
                gradientList.Add(colorKey.color.b);
            }
            photonView.RPC("PropagateGradient", PhotonTargets.Others, gradientList.ToArray());

            // Run for the owner only
            switch (chartType)
            {
                case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                    visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Colour);
                    break;

                case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
                case AbstractVisualisation.VisualisationTypes.FACET:
                    foreach (Chart chart in subCharts)
                    {
                        chart.Gradient = value;
                    }
                    facetSplomKey.UpdateProperties(AbstractVisualisation.PropertyType.None, visualisation);
                    break;
            }
        }
    }

    [PunRPC]
    private void PropagateGradient(float[] gradientArray)
    {
        // Create color keys from received array
        // Values are in order time, r, g, b
        List<GradientColorKey> colorKeys = new List<GradientColorKey>();

        for (int i = 0; i < gradientArray.Length; i += 4)
        {
            Color color = new Color(gradientArray[i + 1], gradientArray[i + 2], gradientArray[i + 3]);

            GradientColorKey colorKey = new GradientColorKey(color, gradientArray[i]);
            colorKeys.Add(colorKey);
        }
        
        Gradient gradient = new Gradient();
        gradient.colorKeys = colorKeys.ToArray();

        visualisation.dimensionColour = gradient;

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Colour);
                break;

            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                facetSplomKey.UpdateProperties(AbstractVisualisation.PropertyType.None, visualisation);
                break;
        }
    }

    public Color[] ColorPalette
    {
        get { return visualisation.coloursPalette; }
        set
        {
            if (value == visualisation.coloursPalette)
                return;

            photonView.RPC("PropagateColorPalette", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateColorPalette(Color[] value)
    {
        visualisation.coloursPalette = value;

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Colour);
                break;

            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.ColorPalette = value;
                }
                facetSplomKey.UpdateProperties(AbstractVisualisation.PropertyType.None, visualisation);
                break;
        }
    }

    public string SizeDimension
    {
        get { return visualisation.sizeDimension; }
        set
        {
            if (value == SizeDimension)
                return;
            
            photonView.RPC("PropagateSizeDimension", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateSizeDimension(string value)
    {
        visualisation.sizeDimension = value;
        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Size);

        switch (chartType)
        {
            //case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
            //    visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Size);
            //    break;

            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                {
                    chart.SizeDimension = value;
                }
                facetSplomKey.UpdateProperties(AbstractVisualisation.PropertyType.None, visualisation);
                break;
        }
    }

    public float Size
    {
        get { return visualisation.size; }
        set
        {
            if (value == Size)
                return;

            photonView.RPC("PropagateSize", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateSize(float value)
    {
        visualisation.size = value;
        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.SizeValues);

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
            case AbstractVisualisation.VisualisationTypes.FACET:
                foreach (Chart chart in subCharts)
                    chart.Size = value;
                break;
        }
    }

    public Vector3 Scale
    {
        get { return new Vector3(visualisation.width, visualisation.height, visualisation.depth); }
        set
        {
            if (value == Scale)
                return;

            photonView.RPC("PropagateScale", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateScale(Vector3 value)
    {
        visualisation.width = value.x;
        visualisation.height = value.y;
        visualisation.depth = value.z;

        CenterVisualisation();
        SetColliderBounds();

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                // Update the axes objects with the length
                GameObject axis = visualisation.theVisualizationObject.X_AXIS;
                if (axis != null)
                {
                    axis.GetComponent<Axis>().Length = value.x;
                    axis.GetComponent<Axis>().UpdateLength();
                }
                axis = visualisation.theVisualizationObject.Y_AXIS;
                if (axis != null)
                {
                    axis.GetComponent<Axis>().Length = value.y;
                    axis.GetComponent<Axis>().UpdateLength();
                }
                axis = visualisation.theVisualizationObject.Z_AXIS;
                if (axis != null)
                {
                    axis.GetComponent<Axis>().Length = value.z;
                    axis.GetComponent<Axis>().UpdateLength();
                }

                visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Scaling);
                ForceViewScale();
                break;

            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
                ResizeAndPositionScatterplotMatrix();
                break;

            case AbstractVisualisation.VisualisationTypes.FACET:
                AdjustAndUpdateFacet();
                break;
        }
    }

    public float Width
    {
        get { return visualisation.width; }
        set
        {
            if (value == Width)
                return;

            photonView.RPC("PropagateWidth", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateWidth(float value)
    {
        visualisation.width = value;
        CenterVisualisation();
        SetColliderBounds();

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                // Update the axis object with the length
                GameObject axis = visualisation.theVisualizationObject.X_AXIS;
                if (axis != null)
                {
                    axis.GetComponent<Axis>().Length = value;
                    axis.GetComponent<Axis>().UpdateLength();
                }

                visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Scaling);
                ForceViewScale();
                break;

            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
                ResizeAndPositionScatterplotMatrix();
                break;

            case AbstractVisualisation.VisualisationTypes.FACET:
                AdjustAndUpdateFacet();
                break;
        }
    }

    public float Height
    {
        get { return visualisation.height; }
        set
        {
            if (value == Height)
                return;

            photonView.RPC("PropagateHeight", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateHeight(float value)
    {
        visualisation.height = value;
        CenterVisualisation();
        SetColliderBounds();

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                // Update the axis object with the length
                GameObject axis = visualisation.theVisualizationObject.Y_AXIS;
                if (axis != null)
                {
                    axis.GetComponent<Axis>().Length = value;
                    axis.GetComponent<Axis>().UpdateLength();
                }

                visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Scaling);
                ForceViewScale();
                break;

            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
                ResizeAndPositionScatterplotMatrix();
                break;

            case AbstractVisualisation.VisualisationTypes.FACET:
                AdjustAndUpdateFacet();
                break;
        }
    }

    public float Depth
    {
        get { return visualisation.depth; }
        set
        {
            if (value == Depth)
                return;

            photonView.RPC("PropagateDepth", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateDepth(float value)
    {
        visualisation.depth = value;
        CenterVisualisation();
        SetColliderBounds();

        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                // Update the axis object with the length
                GameObject axis = visualisation.theVisualizationObject.Z_AXIS;
                if (axis != null)
                {
                    axis.GetComponent<Axis>().Length = value;
                    axis.GetComponent<Axis>().UpdateLength();
                }

                visualisation.updateViewProperties(AbstractVisualisation.PropertyType.Scaling);
                ForceViewScale();
                break;

            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
                ResizeAndPositionScatterplotMatrix();
                break;

            case AbstractVisualisation.VisualisationTypes.FACET:
                AdjustAndUpdateFacet();
                break;
        }
    }

    public AttributeFilter[] AttributeFilters
    {
        get { return visualisation.attributeFilters; }
        set
        {
            if (value == null || value.Length == 0)
                return;

            // TODO: ONLY WORKS WITH ONE FILTER
            AttributeFilter af = value[0];

            photonView.RPC("PropagateAttributeFilters", PhotonTargets.All, af.Attribute, af.minFilter, af.maxFilter, af.minScale, af.maxScale);
        }
    }

    [PunRPC]
    private void PropagateAttributeFilters(string attribute, float minFilter, float maxFilter, float minScale, float maxScale)
    {
        AttributeFilter af = new AttributeFilter()
        {
            Attribute = attribute,
            minFilter = minFilter,
            maxFilter = maxFilter,
            minScale = minScale,
            maxScale = maxScale
        };

        visualisation.attributeFilters = new[] {af};
        visualisation.updateViewProperties(AbstractVisualisation.PropertyType.AttributeFiltering);
    }

    private int scatterplotMatrixSize = 3;
    public int ScatterplotMatrixSize
    {
        get { return scatterplotMatrixSize; }
        set
        {
            value = Mathf.Clamp(value, 2, DataSource.DimensionCount);

            if (value == scatterplotMatrixSize)
                return;

            photonView.RPC("PropagateScatterplotMatrixSize", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateScatterplotMatrixSize(int value)
    {
        if (VisualisationType == AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX)
        {
            scatterplotMatrixSize = value;
            AdjustScatterplotMatrixSize();
        }
    }

    private string facetDimension;
    public string FacetDimension
    {
        get { return facetDimension; }
        set
        {
            if (value == facetDimension)
                return;
            
            photonView.RPC("PropagateFacetDimension", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateFacetDimension(string value)
    {
        facetDimension = value;

        if (VisualisationType == AbstractVisualisation.VisualisationTypes.FACET)
        {
            // Store facet dimension in the first element of AttributeFilters
            visualisation.attributeFilters = new[] { new AttributeFilter() { Attribute = value } };
            facetSplomKey.UpdateProperties(AbstractVisualisation.PropertyType.None, visualisation);

            AdjustAndUpdateFacet();
        }
    }

    private int facetSize = 1;
    public int FacetSize
    {
        get { return facetSize; }
        set
        {
            if (value == facetSize)
                return;

            value = Mathf.Max(value, 1);

            if (value == facetSize)
                return;

            photonView.RPC("PropagateFacetSize", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateFacetSize(int value)
    {
        facetSize = value;

        if (VisualisationType == AbstractVisualisation.VisualisationTypes.FACET)
        {
            AdjustAndUpdateFacet();
        }
    }

    private bool isPrototype;
    public bool IsPrototype
    {
        get { return isPrototype; }
        set
        {
            if (value == isPrototype)
                return;

            photonView.RPC("PropagateIsPrototype", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateIsPrototype(bool value)
    {
        isPrototype = value;
    }

    public bool XAxisVisibility
    {
        get
        {
            return (visualisation.theVisualizationObject.X_AXIS != null &&
                    visualisation.theVisualizationObject.X_AXIS.activeSelf);
        }
        set
        {
            if (value == XAxisVisibility || visualisation.theVisualizationObject.X_AXIS == null)
                return;

            photonView.RPC("PropagateXAxisVisibility", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateXAxisVisibility(bool value)
    {
        visualisation.theVisualizationObject.X_AXIS.SetActive(value);
    }

    public bool YAxisVisibility
    {
        get
        {
            return (visualisation.theVisualizationObject.Y_AXIS != null &&
                    visualisation.theVisualizationObject.Y_AXIS.activeSelf);
        }
        set
        {
            if (value == YAxisVisibility || visualisation.theVisualizationObject.Y_AXIS == null)
                return;

            photonView.RPC("PropagateYAxisVisibility", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateYAxisVisibility(bool value)
    {
        visualisation.theVisualizationObject.Y_AXIS.SetActive(value);
    }

    public bool ZAxisVisibility
    {
        get
        {
            return (visualisation.theVisualizationObject.Z_AXIS != null &&
                    visualisation.theVisualizationObject.Z_AXIS.activeSelf);
        }
        set
        {
            if (value == ZAxisVisibility || visualisation.theVisualizationObject.Z_AXIS == null)
                return;

            photonView.RPC("PropagateZAxisVisibility", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateZAxisVisibility(bool value)
    {
        visualisation.theVisualizationObject.Z_AXIS.SetActive(value);
    }

    public bool KeyVisiblility
    {
        get
        {
            return (visualisation.key != null && visualisation.key.activeSelf);
        }
        set
        {
            if (value == KeyVisiblility || visualisation.key == null)
                return;

            photonView.RPC("PropagateKeyVisibility", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateKeyVisibility(bool value)
    {
        visualisation.key.SetActive(value);
    }

    private bool resizeHandleVisibility = false;
    public bool ResizeHandleVisibility
    {
        get
        {
            return resizeHandleVisibility;
        }
        set
        {
            if (value == resizeHandleVisibility)
                return;

            photonView.RPC("PropagateResizeHandleVisibility", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateResizeHandleVisibility(bool value)
    {
        resizeHandleVisibility = value;

        topLeftInteractableHandle.gameObject.SetActive(value);
        bottomRightInteractableHandle.gameObject.SetActive(value);
    }

    public bool ColliderActiveState
    {
        get { return GetComponent<Collider>().enabled; }
        set
        {
            if (value == ColliderActiveState)
                return;

            photonView.RPC("PropagateColliderActiveState", PhotonTargets.All, value);
        }
    }

    [PunRPC]
    private void PropagateColliderActiveState(bool value)
    {
        GetComponent<Collider>().enabled = value;
    }

    #endregion
    
    private void Awake()
    {
        // Set blank values
        visualisation.colourDimension = "Undefined";
        visualisation.sizeDimension = "Undefined";
        visualisation.linkingDimension = "Undefined";
        visualisation.colorPaletteDimension = "Undefined";

        // Subscribe to events
        interactableObject.InteractableObjectGrabbed += ChartGrabbed;
        interactableObject.InteractableObjectUngrabbed += ChartUngrabbed;

        if (DataSource == null)
            DataSource = ChartManager.Instance.DataSource;

        // If this chart was instantiated by someone else, register it
        if (!photonView.isMine)
        {
            ChartManager.Instance.RegisterVisualisation(this);
        }
    }

    private void SetAsScatterplot()
    {
        // Enable the visualisation
        visualisationGameObject.SetActive(true);

        // Enable this collider
        boxCollider.enabled = true;

        interactableObject.isGrabbable = true;
    }

    private void SetAsScatterplotMatrix()
    {
        // Disable the visualisation
        visualisationGameObject.SetActive(false);

        // Create a new facetSplomKey
        GameObject go = (GameObject)Instantiate(Resources.Load("Key"));
        facetSplomKey = go.GetComponent<Key>();
        facetSplomKey.transform.SetParent(transform);

        // Disable this collider
        boxCollider.enabled = false;

        interactableObject.isGrabbable = false;

        // Create scatterplot matrix gameobjects
        int nbDimensions = DataSource.DimensionCount;
        splomCharts = new Chart[nbDimensions, nbDimensions];
        subCharts = new List<Chart>();
        splomButtons = new SPLOMButton[nbDimensions];

        AdjustScatterplotMatrixSize();

        ResizeAndPositionScatterplotMatrix();
    }

    private void SetAsFacet()
    {
        // Disable the visualisation
        visualisationGameObject.SetActive(false);

        // Create a new facetSplomKey
        GameObject go = (GameObject)Instantiate(Resources.Load("Key"));
        facetSplomKey = go.GetComponent<Key>();
        facetSplomKey.transform.SetParent(transform);

        // Disable this collider
        boxCollider.enabled = false;

        interactableObject.isGrabbable = false;

        // Create facet gameobjects and labels
        subCharts = new List<Chart>();
        facetLabels = new List<GameObject>();

        // Set default faceted dimension
        facetDimension = DataSource[0].Identifier;

        AdjustAndUpdateFacet();
    }

    /// <summary>
    /// Adjusts the number of dimensions by creating/removing subcharts
    /// </summary>
    private void AdjustScatterplotMatrixSize()
    {
        if (!photonView.isMine)
        {
            return;
        }

        // Create scatterplot matrix gameobjects
        int nbDimensions = DataSource.DimensionCount;

        for (int i = 0; i < nbDimensions; i++)
        {
            for (int j = 0; j < nbDimensions; j++)
            {
                // Only add/modify a subchart if [i,j] are smaller than the SPLOM's size
                if (i < scatterplotMatrixSize && j < scatterplotMatrixSize)
                {
                    Chart subChart = splomCharts[i, j];
                    // Only create an object if there wasn't already one
                    if (subChart == null)
                    {
                        // If not along the diagonal, create a chart
                        if (i != j)
                        {
                            subChart = ChartManager.Instance.CreateVisualisation(DataSource[i].Identifier + ", " + DataSource[j].Identifier);

                            subChart.VisualisationType = AbstractVisualisation.VisualisationTypes.SCATTERPLOT;
                            subChart.GeometryType = GeometryType;
                            // Get the x and y dimension from the splom button if it exists, otherwise use default
                            subChart.XDimension = (splomButtons[i] != null) ? splomButtons[i].Text : DataSource[i].Identifier;
                            subChart.YDimension = (splomButtons[j] != null) ? splomButtons[j].Text : DataSource[j].Identifier;
                            subChart.ColorDimension = ColorDimension;
                            subChart.Color = Color;
                            subChart.Gradient = Gradient;
                            subChart.SizeDimension = SizeDimension;
                            subChart.Size = Size;
                            subChart.KeyVisiblility = false;
                            subChart.IsPrototype = true;

                            splomCharts[i, j] = subChart;
                            subChart.transform.SetParent(transform);
                            subCharts.Add(subChart);
                        }
                        // If along the diagonal, create a blank chart (only axis labels with no geometry or collider) and a SPLOM button
                        else
                        {
                            subChart = ChartManager.Instance.CreateVisualisation(DataSource[i].Identifier + ", " + DataSource[j].Identifier);
                            // Get the x and y dimension from the splom button if it exists, otherwise use default
                            subChart.GeometryType = AbstractVisualisation.GeometryType.Undefined;
                            subChart.XDimension = (splomButtons[i] != null) ? splomButtons[i].Text : DataSource[i].Identifier;
                            subChart.YDimension = (splomButtons[j] != null) ? splomButtons[j].Text : DataSource[j].Identifier;
                            subChart.GetComponent<Collider>().enabled = false;
                            subChart.KeyVisiblility = false;
                            subChart.transform.SetParent(transform);
                            splomCharts[i, j] = subChart;

                            GameObject go = PhotonNetwork.Instantiate("SPLOMButton", Vector3.zero, Quaternion.identity, 0);
                            SPLOMButton button = go.GetComponent<SPLOMButton>();
                            button.parentSplomPhotonID = photonView.viewID;
                            button.Text = DataSource[i].Identifier;
                            splomButtons[i] = button;
                            go.transform.SetParent(transform);
                        }
                    }

                    // Hide the axis for all but the charts along the edge
                    bool isAlongLeft = (i == 0);
                    bool isAlongBottom = (j == scatterplotMatrixSize - 1);
                    subChart.XAxisVisibility = isAlongBottom;
                    subChart.YAxisVisibility = isAlongLeft;
                }
                // If it is larger, delete any charts if there were any
                else
                {
                    Chart chart = splomCharts[i, j];

                    if (chart != null)
                    {
                        subCharts.Remove(chart);
                        ChartManager.Instance.RemoveVisualisation(chart);
                    }
                }
            }
        }
        
        // Remove any extra splom buttons
        for (int i = scatterplotMatrixSize; i < splomButtons.Length; i++)
        {
            if (splomButtons[i] != null)
            {
                PhotonNetwork.Destroy(splomButtons[i].gameObject);
                splomButtons[i] = null;
            }
        }

        ResizeAndPositionScatterplotMatrix();
    }

    /// <summary>
    /// Resizes and positions the subcharts in the scatterplot matrix
    /// </summary>
    private void ResizeAndPositionScatterplotMatrix()
    {
        if (!photonView.isMine)
        {
            return;
        }

        if (chartType == AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX && splomCharts != null)
        {
            for (int i = 0; i < scatterplotMatrixSize; i++)
            {
                // Resize splom button
                float w = Width / scatterplotMatrixSize;
                float h = Height / scatterplotMatrixSize;
                float d = Depth / scatterplotMatrixSize;

                // Get the distance from the edge to the centre-point (tentatively called etcp) of each subchart (x and y deltas are the same)
                float delta = Width / (scatterplotMatrixSize * 2);

                // Position such that subcharts start from 1 etcp from the edge, and are spaced two etcp distances from each other
                float x = (-(Width / 2) + delta) + 2 * delta * i;
                float y = ((Height / 2) - delta) - 2 * delta * i;

                SPLOMButton splomButton = splomButtons[i];
                splomButton.transform.localPosition = new Vector3(x, y, 0);
                splomButton.transform.rotation = transform.rotation;
                splomButton.transform.localScale = new Vector3(w * 0.9f, h * 0.9f, 0.05f);

                // Resize charts
                for (int j = 0; j < scatterplotMatrixSize; j++)
                {
                    y = ((Height / 2) - delta) - 2 * delta * j;

                    Chart subChart = splomCharts[i, j];
                    subChart.Scale = new Vector3(w, h, d) * 0.75f;
                    subChart.transform.localPosition = new Vector3(x, y, 0);
                    subChart.transform.rotation = transform.rotation;
                }
            }
        }
    }

    /// <summary>
    /// Readjust the dimensions of the scatterplot matrix
    /// </summary>
    /// <param name="button"></param>
    [PunRPC]
    public void ScatterplotMatrixDimensionChanged(int splomButtonPhotonID, string text)
    {
        if (!photonView.isMine)
        {
            return;
        }

        // Find the original button
        SPLOMButton button = PhotonView.Find(splomButtonPhotonID).GetComponent<SPLOMButton>();

        // Find which index the button belongs to (along the diagonal)
        int index = Array.IndexOf(splomButtons, button);

        // Change y-axis of charts along SPLOM's horizontal
        for (int i = 0; i < scatterplotMatrixSize; i++)
        {
            Chart chart = splomCharts[i, index];

            if (chart.CompareTag("Chart"))
            {
                if (!chart.photonView.isMine)
                    chart.photonView.TransferOwnership(PhotonNetwork.player);

                chart.GetComponent<Chart>().YDimension = text;
            }
        }

        // Change x-axis of charts along SPLOM's vertical
        for (int i = 0; i < scatterplotMatrixSize; i++)
        {
            Chart chart = splomCharts[index, i];

            if (chart.CompareTag("Chart"))
            {
                if (!chart.photonView.isMine)
                    chart.photonView.TransferOwnership(PhotonNetwork.player);

                chart.GetComponent<Chart>().XDimension = text;
            }
        }
    }

    private void AdjustAndUpdateFacet()
    {
        if (!photonView.isMine)
        {
            return;
        }

        for (int i = 0; i < facetSize; i++)
        {
            Chart subChart = subCharts.ElementAtOrDefault(i);

            // Create a chart if it does not already yet exist
            if (subChart == null)
            {
                subChart = ChartManager.Instance.CreateVisualisation(i.ToString());

                subChart.VisualisationType = AbstractVisualisation.VisualisationTypes.SCATTERPLOT;
                subChart.GeometryType = GeometryType;
                subChart.XDimension = XDimension;
                subChart.YDimension = YDimension;
                subChart.SizeDimension = SizeDimension;
                subChart.Size = Size;
                subChart.ColorDimension = ColorDimension;
                subChart.Color = Color;
                subChart.Gradient = Gradient;
                subChart.ColorPaletteDimension = ColorPaletteDimension;
                subChart.ColorPalette = ColorPalette;
                subChart.IsPrototype = true;
                
                subChart.transform.SetParent(transform);
                subCharts.Add(subChart);
            }

            // Set ranges for the chart
            AttributeFilter filter = new AttributeFilter();;
            filter.Attribute = facetDimension;
            filter.minFilter = 0;
            filter.maxFilter = 1;
            filter.minScale = i / (float)facetSize;
            filter.maxScale = (i + 1) / (float)facetSize;
            subChart.AttributeFilters = new AttributeFilter[] {filter};
            subChart.visualisation.updateViewProperties(AbstractVisualisation.PropertyType.AttributeFiltering);
        }

        // Destroy all previous labels (lazy)
        for (int i = facetLabels.Count - 1; i >= 0; i--)
        {
            if (facetLabels[i] != null)
                PhotonNetwork.Destroy(facetLabels[i]);
        }

        // Position the charts
        int index = 0;
        int numRows = (int)Mathf.Sqrt(facetSize);
        int numCols = (int)Mathf.Ceil(facetSize / (float)numRows);

        // Get the distance from the edge to the centre-point (tentatively called etcp) of each subchart
        float xDelta = Width / (numCols * 2);
        float yDelta = Height / (numRows * 2);

        float w = Width / numCols;
        float h = Height / numRows;

        for (int i = 0; i < numRows; i++)
        {
            for (int j = 0; j < numCols; j++)
            {
                if (index < facetSize)
                {
                    Chart subChart = subCharts[index];
                    // Position such that subcharts start from 1 etcp from the edge, and are spaced two etcp distances from each other
                    float x = (-(Width / 2) + xDelta) + 2 * xDelta * j;
                    float y = ((Height / 2) - yDelta) - 2 * yDelta * i;
                    subChart.Scale = new Vector3(w * 0.75f, h * 0.65f, 1);
                    subChart.KeyVisiblility = false;
                    subChart.transform.localPosition = new Vector3(x, y, 0);
                    subChart.transform.rotation = transform.rotation;

                    // Create and position labels
                    if (facetSize > 1)  // Only create if there's more than one facet
                    {
                        GameObject labelHolder = PhotonNetwork.Instantiate("Label", Vector3.zero, Quaternion.identity, 0);
                        facetLabels.Add(labelHolder);
                        labelHolder.transform.SetParent(transform);
                        labelHolder.transform.localPosition = new Vector3(x, y + yDelta * 0.9f, 0);
                        labelHolder.transform.rotation = transform.rotation;

                        NetworkedLabel label = labelHolder.GetComponent<NetworkedLabel>();
                        string range1 = DataSource.getOriginalValue(index / (float)facetSize, FacetDimension).ToString();
                        string range2 = DataSource.getOriginalValue((index + 1) / (float)facetSize, FacetDimension).ToString();
                        label.SetText(range1 + " ... " + range2);
                        label.SetRectTransform(new Vector2(w, 0.1f));
                    }

                    // Hide the axis for all but the charts along the edge
                    bool isAlongLeft = (j == 0);
                    bool isAlongBottom = (i == numRows - 1);

                    // If the the index of a subchart below this one would be larger than the total number of subcharts, then it does not exist, therefore
                    // this subchart is along the bottom
                    int count = index + 1 + numCols;
                    if (count > facetSize)
                        isAlongBottom = true;

                    subChart.XAxisVisibility = isAlongBottom;
                    subChart.YAxisVisibility = isAlongLeft;
                }
                index++;
            }
        }

        // Destroy any extra subcharts
        while (facetSize < subCharts.Count)
        {
            ChartManager.Instance.RemoveVisualisation(subCharts[subCharts.Count - 1]);
            subCharts.Remove(subCharts[subCharts.Count-1]);
        }
    }

    private void ChartGrabbed(object sender, InteractableObjectEventArgs e)
    {
        if (isPrototype)
        {
            originalWorldPos = transform.position;
            originalPos = transform.localPosition;
            originalRot = transform.localRotation;
        }

        previousPosition = transform.position;
        InteractionsManager.Instance.GrabbingStarted();  // TODO: FIX
    }

    private void ChartUngrabbed(object sender, InteractableObjectEventArgs e)
    {
        InteractionsManager.Instance.GrabbingFinished(); // TODO: FIX

        // Animate the work shelf prototype back to its position
        if (isPrototype)
        {
            rigidbody.velocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            AnimateTowards(originalPos, originalRot, 0.4f);
        }
        else
        {
            // Check to see if the chart was thrown
            //Vector3 velocity = VRTK_DeviceFinder.GetControllerVelocity(VRTK_ControllerReference.GetControllerReference(e.interactingObject));
            float speed = rigidbody.velocity.sqrMagnitude;

            if (speed > 10f)
            {
                rigidbody.useGravity = true;
                isThrowing = true;
                deletionTimer = 0;
            }
            else
            {
                rigidbody.velocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;

                // If it wasn't thrown, check to see if it is being placed on the display screen
                if (isTouchingDisplayScreen)
                {
                    AttachToDisplayScreen();
                }
                // If it isn't being placed on the display screen, check if it is being transferred onto a dashboard
                else if (!isPrototype && isTouchingDashboard)
                {
                    touchingDashboard.LoadChart(this, true);
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        //Unsubscribe to events
        interactableObject.InteractableObjectGrabbed -= ChartGrabbed;
        interactableObject.InteractableObjectUngrabbed -= ChartUngrabbed;
    }

    private void FixedUpdate()
    {
        if (interactableObject.IsGrabbed())
            previousPosition = transform.position;
    }

    private void Update()
    {
        if (interactableObject.IsGrabbed())
        {
            // Check if the chart is being pulled from the dashboard
            if (isPrototype && Vector3.Distance(transform.position, originalWorldPos) > 0.25f)
            {
                // Create a duplicate of this visualisation
                Chart dupe = ChartManager.Instance.DuplicateVisualisation(this);

                VRTK_InteractTouch interactTouch = interactableObject.GetGrabbingObject().GetComponent<VRTK_InteractTouch>();
                VRTK_InteractGrab interactGrab = interactableObject.GetGrabbingObject().GetComponent<VRTK_InteractGrab>();

                // Drop this visualisation (it wil return automatically)
                interactGrab.ForceRelease();

                // Grab the duplicate
                interactTouch.ForceTouch(dupe.gameObject);
                interactGrab.AttemptGrab();
            }
            // Check if the chart is being held next to the dashboard for transfer
            else if (!isPrototype && isTouchingDashboard)
            {
                VRTK_ControllerHaptics.TriggerHapticPulse(VRTK_ControllerReference.GetControllerReference(interactableObject.GetGrabbingObject()), 0.4f);
            }
        }
        else if (isThrowing)
        {
            if (1 < deletionTimer)
            {
                isThrowing = false;
                transform.DOScale(0, 1f).OnComplete(() => ChartManager.Instance.RemoveVisualisation(this));
            }
            else
            {
                deletionTimer += Time.deltaTime;
            }
        }

        if (topLeftInteractableHandle.IsGrabbed() || bottomRightInteractableHandle.IsGrabbed())
        {
            if (!photonView.isMine)
                photonView.RequestOwnership();

            Vector3 scale = Scale;

            if (topLeftInteractableHandle.IsGrabbed())
            {
                Vector3 handleLocalPos = transform.InverseTransformPoint(topLeftInteractableHandle.transform.position);

                // Don't allow the handle to get too small
                if (handleLocalPos.y < 0.15f)
                    handleLocalPos.y = 0.15f;

                scale.y = (handleLocalPos.y - 0.08f) * 2;

                // Lock handle to only move along y axis
                handleLocalPos.x = -Width / 2 - 0.05f;
                handleLocalPos.z = 0;
                topLeftInteractableHandle.transform.localPosition = handleLocalPos;
                topLeftInteractableHandle.transform.localRotation = Quaternion.identity;;
            }
            if (bottomRightInteractableHandle.IsGrabbed())
            {
                Vector3 handleLocalPos = transform.InverseTransformPoint(bottomRightInteractableHandle.transform.position);

                // Don't allow the handle to get too small
                if (handleLocalPos.x < 0.15f)
                    handleLocalPos.x = 0.15f;

                scale.x = (handleLocalPos.x - 0.08f) * 2;

                // Lock handle to only move along x axis
                handleLocalPos.y = -Height / 2 - 0.05f;
                handleLocalPos.z = 0;
                bottomRightInteractableHandle.transform.localPosition = handleLocalPos;
                bottomRightInteractableHandle.transform.localRotation = Quaternion.identity; ;
            }

            Scale = scale;
        }
    }
    
    private void ForceViewScale()
    {
        foreach (View view in visualisation.theVisualizationObject.viewList)
        {
            view.transform.localScale = Scale;
        }
    }

    /// <summary>
    /// Sets the size of the collider based on the size and dimensions stored in the Visualisation. This should be called whenever a dimension is added/changed or when
    /// the size is changed.
    /// </summary>
    private void SetColliderBounds()
    {
        float width = visualisation.width;
        float height = visualisation.height;
        float depth = visualisation.depth;

        string x = visualisation.xDimension.Attribute;
        string y = visualisation.yDimension.Attribute;
        string z = visualisation.zDimension.Attribute;
        
        // Calculate size
        float xSize = (x != "Undefined") ? width + 0.015f : 0.1f;
        float ySize = (y != "Undefined") ? height + 0.015f : 0.1f;
        float zSize = (z != "Undefined") ? depth + 0.015f : 0.1f;
        
        boxCollider.size = new Vector3(xSize, ySize, zSize);
        raycastCollider.size = new Vector3(xSize, ySize, 0.01f);

        // Disable colliders if this is not a scatterplot
        if (VisualisationType != AbstractVisualisation.VisualisationTypes.SCATTERPLOT)
        {
            boxCollider.enabled = false;
            raycastCollider.enabled = false;
        }

        // Reposition the resize handles
        topLeftInteractableHandle.transform.localPosition = new Vector3(-width / 2 - 0.05f, height / 2 + 0.08f, 0); 
        bottomRightInteractableHandle.transform.localPosition = new Vector3(width / 2 + 0.08f, -height / 2 - 0.05f, 0);
    }

    private void CenterVisualisation()
    {
        float x = (XDimension != "Undefined") ? Width / 2 : 0;
        float y = (YDimension != "Undefined") ? Height / 2 : 0;
        float z = (ZDimension != "Undefined") ? Depth / 2 : 0;

        //visualisationGameObject.transform.DOLocalMove(new Vector3(-x, -y, -z), 0.1f).SetEase(Ease.OutCubic);
        visualisationGameObject.transform.localPosition = new Vector3(-x, -y, -z);

        // Reposition the key
        switch (chartType)
        {
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT:
                if (visualisation.key != null)
                {
                    visualisation.key.transform.localPosition = new Vector3(0.15f, Height + 0.165f, 0f);
                    visualisation.key.transform.localRotation = Quaternion.identity;
                }
                break;

            case AbstractVisualisation.VisualisationTypes.FACET:
            case AbstractVisualisation.VisualisationTypes.SCATTERPLOT_MATRIX:
                if (facetSplomKey != null)
                {
                    facetSplomKey.transform.localPosition = new Vector3(-0.2f, Height / 2 + 0.105f, 0f);
                    facetSplomKey.transform.localRotation = Quaternion.identity;
                }
                break;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("DisplayScreen"))
        {
            isTouchingDisplayScreen = true;
            touchingDisplayScreen = other.GetComponent<DisplayScreen>();

            // If the chart was thrown at the screen, attach it to the screen
            if (isThrowing)
            {
                isThrowing = false;
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                AttachToDisplayScreen();
            }
        }
        else if (!isPrototype && other.CompareTag("DashboardCollider"))
        {
            isTouchingDashboard = true;
            touchingDashboard = other.transform.parent.GetComponent<Dashboard>();
            
            // If the chart was thrown at the dashboard, immediately transfer it
            if (isThrowing)
            {
                touchingDashboard.LoadChart(this, true);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("DisplayScreen"))
        {
            isTouchingDisplayScreen = false;
            touchingDisplayScreen = null;
        }
        else if (!isPrototype && other.CompareTag("DashboardCollider"))
        {
            isTouchingDashboard = false;
        }
    }

    private void AttachToDisplayScreen()
    {
        Vector3 pos = touchingDisplayScreen.CalculatePositionOnScreen(this);
        Quaternion rot = touchingDisplayScreen.CalculateRotationOnScreen(this);

        AnimateTowards(pos, rot, 0.2f);
    }

    public void AnimateTowards(Vector3 targetPos, Quaternion targetRot, float duration, bool toDestroy = false)
    {
        if (toDestroy)
            transform.DOMove(targetPos, duration).SetEase(Ease.OutQuint).OnComplete(() => ChartManager.Instance.RemoveVisualisation(this));
        else
            transform.DOLocalMove(targetPos, duration).SetEase(Ease.OutQuint).OnComplete(() => photonView.RPC("ForcePosition", photonView.owner, targetPos, targetRot));

        transform.DOLocalRotate(targetRot.eulerAngles, duration).SetEase(Ease.OutQuint);
    }

    /// <summary>
    /// Called when the animation is finished. Used to ensure that the chart returns back to its original position should its owner change mid-animation
    /// </summary>
    /// <param name="targetPos"></param>
    /// <param name="targetRot"></param>
    [PunRPC]
    private void ForcePosition(Vector3 targetPos, Quaternion targetRot)
    {
        transform.localPosition = targetPos;
        transform.localRotation = targetRot;
    }
}
