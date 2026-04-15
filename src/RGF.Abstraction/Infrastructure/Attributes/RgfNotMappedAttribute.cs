using System;
 
namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.Attributes;

/// <summary>
/// Denotes that a property or class should be excluded from RGF Property mapping.
/// </summary>
/// <seealso cref="System.Attribute" />
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class RgfNotMappedAttribute : Attribute 
{ 
}