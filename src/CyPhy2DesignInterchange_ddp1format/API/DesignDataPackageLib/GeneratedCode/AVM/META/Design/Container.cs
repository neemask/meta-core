﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool
//     Changes to this file will be lost if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
namespace AVM.META.Design
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	public abstract partial class Container
	{
		public virtual string Name
		{
			get;
			set;
		}

		public virtual string id
		{
			get;
			set;
		}

		public virtual List<ComponentInstance> ComponentInstances
		{
			get;
			set;
		}

		public virtual List<Container> Containers
		{
			get;
			set;
		}

		public virtual List<ContainerValue> ContainerValues
		{
			get;
			set;
		}

		public virtual List<ContainerPort> ContainerPorts
		{
			get;
			set;
		}

		public virtual List<ContainerStructuralInterface> ContainerStructuralInterfaces
		{
			get;
			set;
		}

	}
}
