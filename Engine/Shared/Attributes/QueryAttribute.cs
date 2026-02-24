using System;

namespace Shared.Attributes;

/// <summary>
/// Marks a field or property as an automated Entity Query.
/// The types to query for are determined by the generic arguments of EntityQuery.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class QueryAttribute : Attribute
{
}
