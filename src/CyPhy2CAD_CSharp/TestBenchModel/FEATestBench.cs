﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using CyPhy = ISIS.GME.Dsml.CyPhyML.Interfaces;
using CyPhyClasses = ISIS.GME.Dsml.CyPhyML.Classes;
using GME.MGA;
using GME.CSharp;
using GME;
using GME.MGA.Meta;
using CyPhy2CAD_CSharp.DataRep;

namespace CyPhy2CAD_CSharp.TestBenchModel
{
    public class FEATestBench : TestBenchBase
    {
        public string SolverType { get; set; }
        public string ElementType { get; set; }
        public string ShellType { get; set; }
        public string MeshType { get; set; }
        public int MaxAdaptiveIterations { get; set; }
        private List<FEALoadBase> Loads;
        private List<FEAConstraintBase> Constraints;
        public List<string> PostProcessScripts { get; set; }
        public List<FEAThermalElement> ThermalElements { get; set; }
        public CyPhyClasses.CADTestBench.AttributesClass.AdjoiningTreatment_enum AdjSurfTreatment;

        protected CyPhy.CADTestBench CyphyTestBenchRef;

        public FEATestBench (CyPhy2CADSettings cadSetting,
                                  string outputdir,
                                  string projectdir,
                                  bool auto = false) :
                                  base(cadSetting, outputdir, projectdir, auto)
        {
            Loads = new List<FEALoadBase>();
            Constraints = new List<FEAConstraintBase>();
            PostProcessScripts = new List<string>();
            ThermalElements = new List<FEAThermalElement>();
        }

        private void CollectLeafComponents(List<CyPhy.Component> result, CyPhy.ComponentAssembly assembly)
        {
            foreach (var compref in assembly.Children.ComponentRefCollection)
            {
                if (compref.AllReferred != null)
                {
                    if (compref.AllReferred is CyPhy.ComponentAssemblyRef)
                    {
                        CollectLeafComponents(result, compref.AllReferred as CyPhy.ComponentAssembly);
                    }
                    else if (compref.AllReferred is CyPhy.Component)
                    {
                        // Interested in components with CAD Model only
                        if ((compref.AllReferred as CyPhy.Component).Children.CADModelCollection.Any())
                            result.Add(compref as CyPhy.Component);
                    }
                }
            }
            foreach (var compass in assembly.Children.ComponentAssemblyCollection)
            {
                CollectLeafComponents(result, compass);
            }
            foreach (var comp in assembly.Children.ComponentCollection)
            {
                // Interested in components with CAD Model only
                if ((comp as CyPhy.Component).Children.CADModelCollection.Any())
                    result.Add(comp);
            }
        }

        private bool GetParamUnitName(CyPhy.Parameter param, ref string unit)
        {
            if (param.AllReferred as CyPhy.unit != null)
            {
                unit = (param.AllReferred as CyPhy.unit).Attributes.Symbol;
                return true;
            } else {
                return false;
            }
        }

        private CyPhy.Parameter GetForceLoadParam(CyPhy.ForceLoadParam forceload, string name, out double param)
        {
            try
            {
                CyPhy.Parameter prm = forceload.Children.ParameterCollection.Where(p => ((MgaFCO)p.Impl).MetaRole.Name == name).First();
                param = String.IsNullOrEmpty(prm.Attributes.Value)?0:double.Parse(prm.Attributes.Value);
                return prm;
            }
            catch (InvalidOperationException)
            {
                Logger.Instance.AddLogMessage("Parameter" + name + " was not found in ForceLoad.", Severity.Error);
            }
            catch (FormatException)
            {
                Logger.Instance.AddLogMessage("Parameter" + name + " is not a valid floating point value.", Severity.Error);
            }
            param = 0;
            return null;
        }

        private CyPhy.Parameter GetAccelerationLoadParam(CyPhy.AccelerationLoadParam accelerationload, string name, out double param)
        {
            try
            {
                CyPhy.Parameter prm = accelerationload.Children.ParameterCollection.Where(p => ((MgaFCO)p.Impl).MetaRole.Name == name).First();
                param = String.IsNullOrEmpty(prm.Attributes.Value)?0:double.Parse(prm.Attributes.Value);
                return prm;
            }
            catch (InvalidOperationException)
            {
                Logger.Instance.AddLogMessage("Parameter" + name + " was not found in AccelerationLoad.", Severity.Error);
            }
            catch (FormatException)
            {
                Logger.Instance.AddLogMessage("Parameter" + name + " is not a valid floating point value.", Severity.Error);
            }
            param = 0;
            return null;
        }

        private CyPhy.Parameter GetPressureLoadParam(CyPhy.PressureLoadParam pressureload, string name, out double param)
        {
            try
            {
                CyPhy.Parameter prm = pressureload.Children.ParameterCollection.Where(p => ((MgaFCO)p.Impl).MetaRole.Name == name).First();
                param = String.IsNullOrEmpty(prm.Attributes.Value) ? 0 : double.Parse(prm.Attributes.Value);
                return prm;
            }
            catch (InvalidOperationException)
            {
                Logger.Instance.AddLogMessage("Parameter" + name + " was not found in PressureLoad.", Severity.Error);
            }
            catch (FormatException)
            {
                Logger.Instance.AddLogMessage("Parameter" + name + " is not a valid floating point value.", Severity.Error);
            }
            param = 0;
            return null;
        }

        public override void TraverseTestBench(CyPhy.TestBenchType testBenchBase)
        {           
            string stepFormat = "AP203_E2_Single_File";
            if (!DataExchangeFormats.Contains(stepFormat))
                DataExchangeFormats.Add(stepFormat);

            CyPhy.CADTestBench testBench = testBenchBase as CyPhy.CADTestBench;
            if (testBench == null)
                testBench = CyPhyClasses.CADTestBench.Cast(testBenchBase.Impl);

            this.CyphyTestBenchRef = testBench;
            base.TraverseTestBench(testBenchBase);

            AdjSurfTreatment = CyphyTestBenchRef.Attributes.AdjoiningTreatment;

            // Solver Settings
            ElementType = "MIDPOINT_PARABOLIC_FIXED";
            ShellType = "N/A";
            SolverType = testBench.Attributes.SolverType.ToString(); 
            MeshType = "SOLID";
            MaxAdaptiveIterations = testBench.Attributes.MaxAdaptiveIterations;
            /*
            ElementType = testBench.Attributes.ElementShapeType.ToString();
            ShellType = (testBench.Attributes.ShellElementType == CyPhyClasses.CADTestBench.AttributesClass.ShellElementType_enum.N_A) ? 
                        testBench.Attributes.ShellElementType.ToString().Replace("_", "/") :
                        testBench.Attributes.ShellElementType.ToString();
            SolverType = testBench.Attributes.SolverType.ToString();

            if (testBench.Attributes.SolverType == CyPhyClasses.CADTestBench.AttributesClass.SolverType_enum.ANSYS)
                throw new NotImplementedException();

            MeshType = testBench.Attributes.MeshType.ToString();         
            MeshType = testBench.Attributes.MeshType.ToString(); 
            */            
            //testBench.Attributes.InfiniteCycle;
            //testBench.Attributes.NumerOfCycles.ToString();


            // Metrics
            foreach (var item in testBench.Children.TIP2StructuralMetricCollection)
            {
                if (item.SrcEnds.TestInjectionPoint != null)
                {
                    CyPhy.TestInjectionPoint tip = item.SrcEnds.TestInjectionPoint;
                    CyPhy.StructuralFEAComputation feaComp = item.DstEnds.StructuralFEAComputation;

                    if (tip.AllReferred == null)
                        continue;

                    List<CyPhy.Component> testComponents = new List<CyPhy.Component>();
                    if (tip.AllReferred is CyPhy.ComponentAssembly)
                    {
                        CollectLeafComponents(testComponents, tip.AllReferred as CyPhy.ComponentAssembly);
                    }
                    else if (tip.AllReferred is CyPhy.Component)
                    {
                        // Interested in components with CAD Model only
                        if ((tip.AllReferred as CyPhy.Component).Children.CADModelCollection.Any())
                            testComponents.Add(tip.AllReferred as CyPhy.Component);
                    }

                    foreach (CyPhy.Component comp in testComponents)
                    {
                        string compId = comp.Attributes.InstanceGUID;

                        foreach (var cyphycompport in feaComp.Children.StructuralAnalysisComputationTypeCollection)
                        {
                            TBComputationType tbcomputation = new TBComputationType();
                            tbcomputation.ComputationType = cyphycompport.Kind.Replace("Stress", "").Replace("Maximum", "");
                            tbcomputation.FeatureDatumName = "";
                            tbcomputation.RequestedValueType = "Scalar";
                            tbcomputation.Details = "InfiniteCycle";
                            tbcomputation.ComponentID = compId;

                            foreach (var cyphyconn in cyphycompport.DstConnections.FEAComputation2MetricCollection)
                            {
                                tbcomputation.MetricID = cyphyconn.DstEnds.Metric.ID;
                            }

                            if (!String.IsNullOrEmpty(tbcomputation.MetricID))
                                this.Computations.Add(tbcomputation);
                        }
                    }
                }
            }

            foreach (var item in testBench.Children.TIP2ThermalMetricCollection)
            {
                if (item.SrcEnds.TestInjectionPoint != null)
                {
                    CyPhy.TestInjectionPoint tip = item.SrcEnds.TestInjectionPoint;
                    CyPhy.ThermalFEAComputation feaComp = item.DstEnds.ThermalFEAComputation;

                    if (tip.AllReferred == null)
                        continue;

                    List<CyPhy.Component> testComponents = new List<CyPhy.Component>();
                    if (tip.AllReferred is CyPhy.ComponentAssembly)
                    {
                        CollectLeafComponents(testComponents, tip.AllReferred as CyPhy.ComponentAssembly);
                    }
                    else if (tip.AllReferred is CyPhy.Component)
                    {
                        // Interested in components with CAD Model only
                        if ((tip.AllReferred as CyPhy.Component).Children.CADModelCollection.Any())
                            testComponents.Add(tip.AllReferred as CyPhy.Component);
                    }

                    foreach (CyPhy.Component comp in testComponents)
                    {
                        string compId = comp.Attributes.InstanceGUID;

                        foreach (var cyphycompport in feaComp.Children.ThermalAnalysisMetricsCollection)
                        {
                            TBComputationType tbcomputation = new TBComputationType();
                            tbcomputation.ComputationType = cyphycompport.Kind;
                            tbcomputation.FeatureDatumName = "";
                            tbcomputation.RequestedValueType = "Scalar";
                            tbcomputation.Details = "InfiniteCycle";
                            tbcomputation.ComponentID = compId;

                            foreach (var cyphyconn in cyphycompport.DstConnections.FEAComputation2MetricCollection)
                            {
                                tbcomputation.MetricID = cyphyconn.DstEnds.Metric.ID;
                            }

                            if (!String.IsNullOrEmpty(tbcomputation.MetricID))
                                this.Computations.Add(tbcomputation);
                        }
                    }
                }
            }

            // thermal elements
            foreach(var item in testBench.Children.ThermalFEAElementsCollection)
            {
                foreach (var conn in item.DstConnections.ThermalElement2TIPCollection)
                {
                    CyPhy.TestInjectionPoint tip = conn.DstEnds.TestInjectionPoint;

                    if (tip.AllReferred == null)
                        continue;

                    List<CyPhy.Component> testComponents = new List<CyPhy.Component>();
                    if (tip.AllReferred is CyPhy.ComponentAssembly)
                    {
                        CollectLeafComponents(testComponents, tip.AllReferred as CyPhy.ComponentAssembly);
                    }
                    else if (tip.AllReferred is CyPhy.Component)
                    {
                        // Interested in components with CAD Model only
                        if ((tip.AllReferred as CyPhy.Component).Children.CADModelCollection.Any())
                            testComponents.Add(tip.AllReferred as CyPhy.Component);
                    }

                    foreach (var component in testComponents)
                    {
                        FEAThermalElement[] element = FEAThermalElement.Extract(item, component.Attributes.InstanceGUID, null);
                        ThermalElements.AddRange(element);
                    }
                }

                foreach (var conn in item.DstConnections.ThermalElements2GeometryCollection)
                {
                    CyPhy.GeometryBase geometryBase = conn.DstEnds.GeometryTypes;
                    string tipContextPath = Path.GetDirectoryName(geometryBase.Path);
                    CADGeometry geometryRep = FillOutGeometryRep(geometryBase.Impl as MgaFCO,
                                                                 tipContextPath);
                    FEAThermalElement[] element = FEAThermalElement.Extract(item, null, geometryRep);
                    ThermalElements.AddRange(element);
                }
            }

            if (testBench.Children.ThermalEnvironmentCollection.Any())
            {
                if (testBench.Children.ThermalEnvironmentCollection.Count() > 1)
                {
                    Logger.Instance.AddLogMessage("Multiple ThermalEnvironments are present in the testbench. There should be only one.", Severity.Error);
                }
                else
                {
                    if (!testBench.Children.ThermalEnvironmentCollection.First().Children.ParameterCollection.Any())
                    {
                        Logger.Instance.AddLogMessage("ThermalEnvironment is present but there are no parameters specified in it.", Severity.Warning);
                    }
                    else
                    {
                        foreach (var param in testBench.Children.ThermalEnvironmentCollection.First().Children.ParameterCollection)
                        {
                            var elem = new FEAThermalElement(param) { Unit = "C", ComponentID = cadDataContainer.assemblies.First().Key };
                            ThermalElements.Add(elem);
                        }
                    }
                }
            }

            // Constraints
            foreach (var cyphyconstraint in testBench.Children.AnalysisConstraintCollection)
            {
                if (cyphyconstraint.Kind == "PinConstraint")
                {
                    CyPhy.PinConstraint pinConstraint = CyPhyClasses.PinConstraint.Cast(cyphyconstraint.Impl);

                    // Geometry - must be a cylinder
                    foreach (var geometry in pinConstraint.DstConnections.Pin2CylinderCollection)
                    {
                        FEAPinConstraint feapinRep = new FEAPinConstraint();
                        feapinRep.AxialDisplacement = pinConstraint.Attributes.AxialDisplacement.ToString();
                        feapinRep.AxialRotation = pinConstraint.Attributes.AxialRotation.ToString();

                        CyPhy.CylinderGeometryType cylinderType = geometry.DstEnds.CylinderGeometryType;
                        if (cylinderType != null)
                        {
                            string tipContextPath = Path.GetDirectoryName(cylinderType.Path);
                            AddGeometry2Constraint(feapinRep,
                                                   cylinderType.Impl as MgaFCO,
                                                   tipContextPath);
                        }
                    }

                }
                else if (cyphyconstraint.Kind == "BallConstraint")
                {
                    CyPhy.BallConstraint ballConstraint = CyPhyClasses.BallConstraint.Cast(cyphyconstraint.Impl);

                    foreach (var item in ballConstraint.DstConnections.Ball2SphereCollection)
                    {
                        FEABallConstraint feaballRep = new FEABallConstraint();
                        CyPhy.SphereGeometryType sphereType = item.DstEnds.SphereGeometryType;
                        if (sphereType != null)
                        {
                            string tipContextPath = Path.GetDirectoryName(sphereType.Path);
                            AddGeometry2Constraint(feaballRep,
                                                   sphereType.Impl as MgaFCO,
                                                   tipContextPath);
                        }
                    }
                }
                else if (cyphyconstraint.Kind == "DisplacementConstraint")
                {
                    CyPhy.DisplacementConstraint displacementConstraint = CyPhyClasses.DisplacementConstraint.Cast(cyphyconstraint.Impl);                    

                    string tx = "FREE", ty = "FREE", tz = "FREE", tunit = "mm", rx = "FREE", ry = "FREE", rz = "FREE", runit = "deg";

                    CyPhy.Rotation rotation = displacementConstraint.Children.RotationCollection.FirstOrDefault();
                    if (rotation != null)
                    {
                        bool hasScalar = (rotation.Attributes.XDirection == CyPhyClasses.Rotation.AttributesClass.XDirection_enum.SCALAR) ||
                                         (rotation.Attributes.YDirection == CyPhyClasses.Rotation.AttributesClass.YDirection_enum.SCALAR) ||
                                         (rotation.Attributes.ZDirection == CyPhyClasses.Rotation.AttributesClass.ZDirection_enum.SCALAR) ;
                        rx = (rotation.Attributes.XDirection == CyPhyClasses.Rotation.AttributesClass.XDirection_enum.SCALAR) ? 
                              rotation.Attributes.XDirectionValue.ToString() : rotation.Attributes.XDirection.ToString();
                        ry = (rotation.Attributes.YDirection == CyPhyClasses.Rotation.AttributesClass.YDirection_enum.SCALAR) ?
                              rotation.Attributes.YDirectionValue.ToString() : rotation.Attributes.YDirection.ToString();
                        rz = (rotation.Attributes.ZDirection == CyPhyClasses.Rotation.AttributesClass.ZDirection_enum.SCALAR) ?
                              rotation.Attributes.ZDirectionValue.ToString() : rotation.Attributes.ZDirection.ToString();

                        if (!hasScalar)
                        {
                            runit = "N/A";
                        }
                        else
                        {
                            if (rotation.Referred.unit != null)
                                runit = rotation.Referred.unit.Name;
                        }
                    }

                    CyPhy.Translation translation = displacementConstraint.Children.TranslationCollection.FirstOrDefault();
                    if (translation != null)
                    {
                        bool hasScalar = (translation.Attributes.XDirection == CyPhyClasses.Translation.AttributesClass.XDirection_enum.SCALAR) ||
                                         (translation.Attributes.YDirection == CyPhyClasses.Translation.AttributesClass.YDirection_enum.SCALAR) ||
                                         (translation.Attributes.ZDirection == CyPhyClasses.Translation.AttributesClass.ZDirection_enum.SCALAR);
                        tx = (translation.Attributes.XDirection == CyPhyClasses.Translation.AttributesClass.XDirection_enum.SCALAR) ?
                              translation.Attributes.XDirectionValue.ToString() : translation.Attributes.XDirection.ToString();
                        ty = (translation.Attributes.YDirection == CyPhyClasses.Translation.AttributesClass.YDirection_enum.SCALAR) ?
                              translation.Attributes.YDirectionValue.ToString() : translation.Attributes.YDirection.ToString();
                        tz = (translation.Attributes.ZDirection == CyPhyClasses.Translation.AttributesClass.ZDirection_enum.SCALAR) ?
                              translation.Attributes.ZDirectionValue.ToString() : translation.Attributes.ZDirection.ToString();

                        if (!hasScalar)
                        {
                            tunit = "N/A";
                        }
                        else
                        {
                            if (translation.Referred.unit != null)
                                tunit = translation.Referred.unit.Name;
                        }
                        
                    }


                    foreach (var item in displacementConstraint.DstConnections.Displacement2GeometryCollection)
                    {
                        FEADisplacementConstraint feadispRep = new FEADisplacementConstraint();
                        feadispRep.Rotation_X = rx;
                        feadispRep.Rotation_Y = ry;
                        feadispRep.Rotation_Z = rz;
                        feadispRep.RotationUnits = runit;
                        feadispRep.Translation_X = tx;
                        feadispRep.Translation_Y = ty;
                        feadispRep.Translation_Z = tz;
                        feadispRep.TranslationUnits = tunit;

                        Logger.Instance.AddLogMessage(String.Format("DisplacementConstraint Units - Rotation Component = {0}, Translation Component = {1}", runit, tunit), Severity.Info);

                        CyPhy.GeometryBase geometry = item.DstEnds.GeometryBase;
                        if (geometry != null)
                        {
                            string tipContextPath = Path.GetDirectoryName(geometry.Path);
                            AddGeometry2Constraint(feadispRep,
                                                   geometry.Impl as MgaFCO,
                                                   tipContextPath);
                        }
                    }
                }
            }

            // Loads
            foreach (var cyphyload in testBench.Children.AnalysisLoadCollection)
            {
                if (cyphyload is CyPhy.ForceLoadParam)
                {
                    CyPhy.ForceLoadParam forceLoad = CyPhyClasses.ForceLoadParam.Cast(cyphyload.Impl);

                    double fx = 0.0, fy = 0.0, fz = 0.0, mx = 0.0, my = 0.0, mz = 0.0;
                    string funit = "N", munit = "N-mm";

                    CyPhy.Parameter p1 = GetForceLoadParam(forceLoad, "ForceX", out fx);
                    GetForceLoadParam(forceLoad, "ForceY", out fy);
                    GetForceLoadParam(forceLoad, "ForceZ", out fz);
                    CyPhy.Parameter p2 = GetForceLoadParam(forceLoad, "MomentX", out mx);
                    GetForceLoadParam(forceLoad, "MomentY", out my);
                    GetForceLoadParam(forceLoad, "MomentZ", out mz);

                    GetParamUnitName(p1, ref funit);
                    GetParamUnitName(p2, ref munit);


                    foreach (var item in forceLoad.DstConnections.ForceLoadParam2GeometryCollection)

                    {
                        FEAForceLoad feaforceRep = new FEAForceLoad();
                        feaforceRep.Force_X = fx;
                        feaforceRep.Force_Y = fy;
                        feaforceRep.Force_Z = fz;
                        feaforceRep.ForceUnit = funit;
                        feaforceRep.Moment_X = mx;
                        feaforceRep.Moment_Y = my;
                        feaforceRep.Moment_Z = mz;
                        feaforceRep.MomentUnit = munit;

                        Logger.Instance.AddLogMessage(String.Format("ForceLoad Units - Force Component = {0}, Moment Component = {1}", funit, munit), Severity.Info);

                        CyPhy.GeometryBase geometry = item.DstEnds.GeometryBase;
                        if (geometry != null)
                        {
                            string tipContextPath = Path.GetDirectoryName(geometry.Path);
                            AddGeometry2Load(feaforceRep,
                                             geometry.Impl as MgaFCO,
                                             tipContextPath);
                        }
                    }
                }
                else
                if (cyphyload is CyPhy.ForceLoad)
                {
                    Logger.Instance.AddLogMessage("ForceLoad is used in FEA testbench. This construct is obsolete, please use ForceLoadParam instead.", Severity.Warning);
                    CyPhy.ForceLoad forceLoad = CyPhyClasses.ForceLoad.Cast(cyphyload.Impl);
                    
                    double fx = 0.0, fy = 0.0, fz = 0.0, mx = 0.0, my = 0.0, mz = 0.0;
		            string funit = "N", munit = "N-mm";

                    CyPhy.Force force = forceLoad.Children.ForceCollection.FirstOrDefault();
                    if (force != null)
                    {
                        fx = force.Attributes.XDirectionValue;
                        fy = force.Attributes.YDirectionValue;
                        fz = force.Attributes.ZDirectionValue;
                        if (force.Referred.unit != null)
                            funit = force.Referred.unit.Name;
                    }

                    CyPhy.Moment moment = forceLoad.Children.MomentCollection.FirstOrDefault();
                    if (moment != null)
                    {
                        mx = moment.Attributes.XDirectionValue;
                        my = moment.Attributes.YDirectionValue;
                        mz = moment.Attributes.ZDirectionValue;
                        if (moment.Referred.unit != null)
                            munit = moment.Referred.unit.Name;
                    }


                    foreach (var item in forceLoad.DstConnections.Force2GeometryCollection)
                    {
                        FEAForceLoad feaforceRep = new FEAForceLoad();
                        feaforceRep.Force_X = fx;
                        feaforceRep.Force_Y = fy;
                        feaforceRep.Force_Z = fz;
                        feaforceRep.ForceUnit = funit;
                        feaforceRep.Moment_X = mx;
                        feaforceRep.Moment_Y = my;
                        feaforceRep.Moment_Z = mz;
                        feaforceRep.MomentUnit = munit;

                        Logger.Instance.AddLogMessage(String.Format("ForceLoad Units - Force Component = {0}, Moment Component = {1}", funit, munit), Severity.Info);

                        CyPhy.GeometryBase geometry = item.DstEnds.GeometryBase;
                        if (geometry != null)
                        {
                            string tipContextPath = Path.GetDirectoryName(geometry.Path);
                            AddGeometry2Load(feaforceRep,
                                             geometry.Impl as MgaFCO,
                                             tipContextPath);
                        }
                    }

                }
                else if (cyphyload is CyPhy.AccelerationLoadParam)
                {
                    CyPhy.AccelerationLoadParam acceleration = CyPhyClasses.AccelerationLoadParam.Cast(cyphyload.Impl);
                    FEAccelerationLoad feaaccelRep = new FEAccelerationLoad();
                    double x = 0;
                    double y = 0;
                    double z = 0;
                    CyPhy.Parameter p1 = GetAccelerationLoadParam(acceleration, "X", out x);
                    GetAccelerationLoadParam(acceleration, "Y", out y);
                    GetAccelerationLoadParam(acceleration, "Z", out z);
                    feaaccelRep.X = x;
                    feaaccelRep.Y = y;
                    feaaccelRep.Z = z;

                    string unit = "mm/s^2";
                    GetParamUnitName(p1, ref unit);
                    feaaccelRep.Units = unit;

                    Logger.Instance.AddLogMessage(String.Format("AccelerationLoad Units = {0}", feaaccelRep.Units), Severity.Info);

                    this.Loads.Add(feaaccelRep);

                }
                else if (cyphyload is CyPhy.AccelerationLoad)
                {
                    Logger.Instance.AddLogMessage("AccelerationLoad is used in FEA testbench. This construct is obsolete, please use AccelerationLoadParam instead.", Severity.Warning);
                    CyPhy.AccelerationLoad acceleration = CyPhyClasses.AccelerationLoad.Cast(cyphyload.Impl);
                    FEAccelerationLoad feaaccelRep = new FEAccelerationLoad();
                    feaaccelRep.X = acceleration.Attributes.XDirectionValue;
                    feaaccelRep.Y = acceleration.Attributes.YDirectionValue;
                    feaaccelRep.Z = acceleration.Attributes.ZDirectionValue;
                    if (acceleration.Referred.unit != null)
                        feaaccelRep.Units = acceleration.Referred.unit.Name;
                    else
                        feaaccelRep.Units = "mm/s^2";

                    Logger.Instance.AddLogMessage(String.Format("AccelerationLoad Units = {0}", feaaccelRep.Units), Severity.Info);

                    this.Loads.Add(feaaccelRep);
                }
                else if (cyphyload is CyPhy.PressureLoadParam)
                {
                    CyPhy.PressureLoadParam pressure = CyPhyClasses.PressureLoadParam.Cast(cyphyload.Impl);

                    foreach (var item in pressure.DstConnections.PressureParam2GeometryCollection)
                    {
                        FEAPressureLoad feapressRep = new FEAPressureLoad();
                        double p = 0;
                        CyPhy.Parameter p1 = GetPressureLoadParam(pressure, "PressureLoad", out p);
                        feapressRep.Value = p;
                        string unit = "MPa";
                        GetParamUnitName(p1, ref unit);
                        feapressRep.Units = unit;

                        Logger.Instance.AddLogMessage(String.Format("PressureLoad Units = {0}", feapressRep.Units), Severity.Info);

                        CyPhy.GeometryBase geometry = item.DstEnds.GeometryBase;
                        if (geometry != null)
                        {
                            string tipContextPath = Path.GetDirectoryName(geometry.Path);
                            AddGeometry2Load(feapressRep,
                                             geometry.Impl as MgaFCO,
                                             tipContextPath);
                        }
                    }
                }
                else if (cyphyload is CyPhy.PressureLoad)
                {
                    Logger.Instance.AddLogMessage("PressureLoad is used in FEA testbench. This construct is obsolete, please use PressureLoadParam instead.", Severity.Warning);

                    CyPhy.PressureLoad pressure = CyPhyClasses.PressureLoad.Cast(cyphyload.Impl);

                    foreach (var item in pressure.DstConnections.Pressure2GeometryCollection)
                    {
                        FEAPressureLoad feapressRep = new FEAPressureLoad();
                        feapressRep.Value = pressure.Attributes.Value;
                        if (pressure.Referred.unit != null)
                            feapressRep.Units = pressure.Referred.unit.Name;
                        else
                            feapressRep.Units = "MPa";

                        Logger.Instance.AddLogMessage(String.Format("PressureLoad Units = {0}", feapressRep.Units), Severity.Info);

                        CyPhy.GeometryBase geometry = item.DstEnds.GeometryBase;
                        if (geometry != null)
                        {
                            string tipContextPath = Path.GetDirectoryName(geometry.Path);
                            AddGeometry2Load(feapressRep,
                                             geometry.Impl as MgaFCO,
                                             tipContextPath);
                        }
                    }
                }
            }
            // Post Processing Blocks
            foreach (var postprocess in testBench.Children.PostProcessingCollection)
            {
                PostProcessScripts.Add(postprocess.Attributes.ScriptPath);
            }


        }

        /*
        private void AddGeometry2Constraint(FEAConstraintBase constraintRep, 
                                            MgaFCO geometryFCO,
                                            string tipContextPath)
        {
            GeometryTraversal traverser = new GeometryTraversal();
            traverser.TraverseGeometry(geometryFCO);

            foreach (var geometryFound in traverser.geometryFound)
            {
                CyPhy.GeometryTypes geometryCyPhy = CyPhyClasses.GeometryTypes.Cast(geometryFound);
                if (Path.GetDirectoryName(geometryCyPhy.Path).Contains(tipContextPath))            // within context of TIP
                {
                    FEAGeometry geomRep = CreateGeometry(geometryCyPhy);
                    if (geomRep != null)
                    {
                        constraintRep.AddGeometry(geomRep);
                        this.Constraints.Add(constraintRep);
                    }
                }
            }
        }

        private void AddGeometry2Load(FEALoadBase loadRep,
                                      MgaFCO geometryFCO,
                                      string tipContextPath)
        {
            GeometryTraversal traverser = new GeometryTraversal();
            traverser.TraverseGeometry(geometryFCO);

            foreach (var geometryFound in traverser.geometryFound)
            {
                CyPhy.GeometryTypes geometryCyPhy = CyPhyClasses.GeometryTypes.Cast(geometryFound);
                if (Path.GetDirectoryName(geometryCyPhy.Path).Contains(tipContextPath))            // within context of TIP
                {
                    FEAGeometry geomRep = CreateGeometry(geometryCyPhy);
                    if (geomRep != null)
                    {
                        loadRep.AddGeometry(geomRep);
                        this.Loads.Add(loadRep);
                    }
                }
            }
        }
        */

        private void AddGeometry2Constraint(FEAConstraintBase constraintRep,
                                    MgaFCO geometryFCO,
                                    string tipContextPath)
        {
            GeometryTraversal traverser = new GeometryTraversal();
            traverser.TraverseGeometry(geometryFCO);

            CADGeometry geomRep = FillOutGeometryRep(geometryFCO,
                                                     tipContextPath);
            if (geomRep != null)
            {
                constraintRep.AddGeometry(geomRep);
                this.Constraints.Add(constraintRep);
            }           
            
        }

        private void AddGeometry2Load(FEALoadBase loadRep,
                                      MgaFCO geometryFCO,
                                      string tipContextPath)
        {
            GeometryTraversal traverser = new GeometryTraversal();
            traverser.TraverseGeometry(geometryFCO);
            
            CADGeometry geomRep = FillOutGeometryRep(geometryFCO,
                                                     tipContextPath);
            if (geomRep != null)
            {                     
                loadRep.AddGeometry(geomRep);
                this.Loads.Add(loadRep);
            }

        }

        private CADGeometry FillOutGeometryRep(MgaFCO geometryFCO,
                                        string tipContextPath)
        {
            GeometryTraversal traverser = new GeometryTraversal();
            traverser.TraverseGeometry(geometryFCO);

            if (traverser.geometryFound.Count > 0)
            {
                CyPhy.GeometryTypes geometryCyPhy = CyPhyClasses.GeometryTypes.Cast(traverser.geometryFound.First());
                if (Path.GetDirectoryName(geometryCyPhy.Path).Contains(tipContextPath))            // within context of TIP
                {
                    CADGeometry geomRep = CADGeometry.CreateGeometry(geometryCyPhy);
                    return geomRep;
                }
                else
                    return null;
            }
            else
                return null;
        }

        public override bool GenerateOutputFiles()
        {
            if (!HasErrors())
            {
                GenerateCADXMLOutput();
                GenerateRunBat();
                GenerateScriptFiles();
                GenerateProcessingScripts(PostProcessScripts);
                return true;
            }
            return false;
        }

        public override void GenerateCADXMLOutput()
        {
            CAD.AssembliesType assembliesoutroot = cadDataContainer.ToCADXMLOutput(this);

            if (assembliesoutroot.Assembly.Length > 0)
            {
                AddAnalysisToXMLOutput(assembliesoutroot.Assembly[0]);
            }
                       

            // materials
            /*
            // META-1544: removed
            List<CAD.MaterialType> materialList = new List<CAD.MaterialType>();
            CAD.MaterialsType materials = new CAD.MaterialsType();
            materials._id = UtilityHelpers.MakeUdmID();
            foreach (var item in cadDataContainer.CreateMaterialList())
            {
                CAD.MaterialType material = new CAD.MaterialType();
                material._id = UtilityHelpers.MakeUdmID();
                material.MaterialID = item.Key;
                material.MaterialName = item.Value;
                material.MaterialType1 = "";
                materialList.Add(material);
            }
            materials.Material = materialList.ToArray();
            assembliesoutroot.Materials = new CAD.MaterialsType[1];
            assembliesoutroot.Materials[0] = materials;
            */

            AddDataExchangeFormatToXMLOutput(assembliesoutroot);
            assembliesoutroot.SerializeToFile(Path.Combine(OutputDirectory, TestBenchBase.CADAssemblyFile));

        }

        protected override void AddAnalysisToXMLOutput(CAD.AssemblyType assembly)
        {
            base.AddAnalysisToXMLOutput(assembly);
            CAD.AnalysesType cadanalysis = GetCADAnalysis(assembly);

            CAD.FEAType feaanalysis = new CAD.FEAType();
            feaanalysis._id = UtilityHelpers.MakeUdmID();
            feaanalysis.AnalysisID = AnalysisID;
            feaanalysis.Type = "STRUCTURAL";
            feaanalysis.MaxAdaptiveIterations = MaxAdaptiveIterations;
            feaanalysis.MaxElementSize = this.CyphyTestBenchRef.Attributes.MaximumElementSize;
            // solvers
            CAD.SolversType solversType = new CAD.SolversType();
            solversType._id = UtilityHelpers.MakeUdmID();
            CAD.SolverType solver = new CAD.SolverType();
            solver._id = UtilityHelpers.MakeUdmID();
            solver.ElementShapeType = ElementType;
            solver.MeshType = MeshType;
            solver.ShellElementType = ShellType;
            solver.Type = SolverType;
            solversType.Solver = new CAD.SolverType[1];
            solversType.Solver[0] = solver;
            feaanalysis.Solvers = solversType;

            // loads
            if (Loads.Count > 0)
            {
                List<CAD.LoadType> loadList = new List<CAD.LoadType>();
                foreach (var item in Loads)
                {
                    loadList.Add(item.ToCADXMLOutput());
                }

                feaanalysis.Loads = new CAD.LoadsType();
                feaanalysis.Loads._id = UtilityHelpers.MakeUdmID();
                feaanalysis.Loads.Load = loadList.ToArray();
            }

            // constraints
            if (Constraints.Count > 0)
            {
                List<CAD.AnalysisConstraintType> constraintList = new List<CAD.AnalysisConstraintType>();
                foreach (var item in Constraints)
                {
                    constraintList.Add(item.ToCADXMLOutput());
                }
                feaanalysis.AnalysisConstraints = new CAD.AnalysisConstraintsType();
                feaanalysis.AnalysisConstraints._id = UtilityHelpers.MakeUdmID();
                feaanalysis.AnalysisConstraints.AnalysisConstraint = constraintList.ToArray();
            }
            
            // thermal
            List<CAD.ThermalElementType> thermalOutList = new List<CAD.ThermalElementType>();
            if (ThermalElements.Count > 0)
            {
                foreach (var thermalLoad in ThermalElements)
                {
                    CAD.ThermalElementType thermalOut = new CAD.ThermalElementType();
                    thermalOut._id = UtilityHelpers.MakeUdmID();
                    thermalOut.LoadType = thermalLoad.Type;
                    thermalOut.Unit = thermalLoad.Unit;
                    thermalOut.Value = thermalLoad.LoadValue;

                    if (thermalLoad.Geometry == null)
                    {
                        CAD.ComponentType component = new CAD.ComponentType();
                        component._id = UtilityHelpers.MakeUdmID();
                        component.ComponentID = thermalLoad.ComponentID;
                        component.InfiniteCycle = false;
                        thermalOut.Component = new CAD.ComponentType[] { component };
                    }
                    else
                    {
                        thermalOut.Geometry = new CAD.GeometryType[] { thermalLoad.Geometry.ToCADXMLOutput() };
                    }

                    thermalOutList.Add(thermalOut);
                }
                feaanalysis.ThermalElements = new CAD.ThermalElementsType();
                feaanalysis.ThermalElements._id = UtilityHelpers.MakeUdmID();
                feaanalysis.ThermalElements.ThermalElement = thermalOutList.ToArray();
                
            }
             
            // metrics
            List<CAD.MetricType> metriclist = new List<CAD.MetricType>();
            foreach (var item in Computations)
            {
                CAD.MetricType metric = new CAD.MetricType();
                metric._id = UtilityHelpers.MakeUdmID();
                metric.ComponentID = item.ComponentID;
                metric.MetricID = item.MetricID;
                metric.RequestedValueType = "Scalar";
                metric.MetricType1 = item.ComputationType;
                metric.Details = "";
                metriclist.Add(metric);
            }
            if (metriclist.Any())
            {
                feaanalysis.Metrics = new CAD.MetricsType();
                feaanalysis.Metrics._id = UtilityHelpers.MakeUdmID();
                feaanalysis.Metrics.Metric = metriclist.ToArray();
            }

            cadanalysis.FEA = new CAD.FEAType[] { feaanalysis };
        }

        
        public override void GenerateRunBat()
        {
            StringBuilder sbuilder = new StringBuilder();
            sbuilder.AppendLine();
            sbuilder.AppendLine("REM ****************************");
            sbuilder.AppendFormat("REM {0} Tool\n",
                                  SolverType);
            sbuilder.AppendLine("REM ****************************");


            if (SolverType == CyPhyClasses.CADTestBench.AttributesClass.SolverType_enum.ABAQUS_Model_Based.ToString())
            {
                sbuilder.AppendLine("set FEA_SCRIPT=\"%PROE_ISIS_EXTENSIONS%\\bin%\\Abaqus\\AbaqusMain.py\"\n");
            }
            else if (SolverType == CyPhyClasses.CADTestBench.AttributesClass.SolverType_enum.ABAQUS_Deck_Based.ToString())
            {
                sbuilder.AppendLine("set OLDDIR=%cd%");
                sbuilder.AppendLine("cd Analysis\\Abaqus");
                sbuilder.AppendLine("set FEA_SCRIPT=\"runAnalysis.bat\"\n");
            }
            else if (SolverType == CyPhyClasses.CADTestBench.AttributesClass.SolverType_enum.NASTRAN.ToString())
            {
                sbuilder.AppendLine("set FEA_SCRIPT=\"Analysis\\Nastran\\runAnalysis.bat\"\n");
            }

            sbuilder.AppendLine("if exist %FEA_SCRIPT% goto  :FEA_FOUND");
            sbuilder.AppendLine("@echo off");
            sbuilder.AppendLine("echo		Error: Could not find %FEA_SCRIPT%.");
            sbuilder.AppendLine("echo		Your system is not properly configured to run %FEA_SCRIPT%.");
            sbuilder.AppendLine("set ERROR_CODE=2");
            sbuilder.AppendLine("set ERROR_MSG=\"Error: Could not find %FEA_SCRIPT%.\"");
            sbuilder.AppendLine("goto :ERROR_SECTION\n");

            sbuilder.AppendLine(":FEA_FOUND");
            if (SolverType == CyPhyClasses.CADTestBench.AttributesClass.SolverType_enum.ABAQUS_Model_Based.ToString())
            {
                switch (CyphyTestBenchRef.Attributes.FEAMode)
                {
                    case CyPhyClasses.CADTestBench.AttributesClass.FEAMode_enum.Meshing_Only:
                        sbuilder.AppendLine("call abaqus cae noGUI=%FEA_SCRIPT% -- -o\n");
                        break;
                    case CyPhyClasses.CADTestBench.AttributesClass.FEAMode_enum.Meshing_and_Boundary_Conditions:
                        sbuilder.AppendLine("call abaqus cae noGUI=%FEA_SCRIPT% -- -b\n");
                        break;
                    case CyPhyClasses.CADTestBench.AttributesClass.FEAMode_enum.Modal:
                        sbuilder.AppendLine("call abaqus cae noGUI=%FEA_SCRIPT% -- -m\n");
                        break;
                    case CyPhyClasses.CADTestBench.AttributesClass.FEAMode_enum.Dynamic__Explicit_:
                        sbuilder.AppendLine("call abaqus cae noGUI=%FEA_SCRIPT% -- -e\n");
                        break;
                    case CyPhyClasses.CADTestBench.AttributesClass.FEAMode_enum.Dynamic__Implicit_:
                        sbuilder.AppendLine("call abaqus cae noGUI=%FEA_SCRIPT% -- -i\n");
                        break;
                    case CyPhyClasses.CADTestBench.AttributesClass.FEAMode_enum.Static__Standard_:
                        sbuilder.AppendLine("call abaqus cae noGUI=%FEA_SCRIPT% -- -s\n");
                        break;
                }
            }
            else if (SolverType == CyPhyClasses.CADTestBench.AttributesClass.SolverType_enum.ABAQUS_Deck_Based.ToString())
            {
                sbuilder.AppendLine("cmd /c %FEA_SCRIPT%\n");
                sbuilder.AppendLine("cd %OLDDIR%");
            }
            else if (SolverType == CyPhyClasses.CADTestBench.AttributesClass.SolverType_enum.NASTRAN.ToString())
            {
                sbuilder.AppendLine("cmd /c %FEA_SCRIPT%\n");
            }

            sbuilder.AppendLine("set ERROR_CODE=%ERRORLEVEL%");
            sbuilder.AppendLine("if %ERRORLEVEL% NEQ 0 (");

            if (SolverType == CyPhyClasses.CADTestBench.AttributesClass.SolverType_enum.ABAQUS_Model_Based.ToString())
                sbuilder.AppendLine("set ERROR_MSG=\"Script Error: error level is %ERROR_CODE%, see log/CyPhy2AbaqusCmd.log for details.\"");
            else
                sbuilder.AppendLine("set ERROR_MSG=\"%FEA_SCRIPT% encountered error during execution, error level is %ERROR_CODE%\"");
            sbuilder.AppendLine("goto :ERROR_SECTION");
            sbuilder.AppendLine(")");

            Template.run_bat searchmeta = new Template.run_bat()
            {
                Automation = IsAutomated,
                XMLFileName = "CADAssembly",
                ComputedMetricsPath = "\"Analysis\\Abaqus\\ComputedValues.xml\"",
                AdditionalOptions = CADOptions??"",
                CallDomainTool = sbuilder.ToString()
            };
            using (StreamWriter writer = new StreamWriter(Path.Combine(OutputDirectory, "runCreateCADAssembly.bat")))
            {
                writer.WriteLine(searchmeta.TransformText());
            }
            
        }     
       

    }
}