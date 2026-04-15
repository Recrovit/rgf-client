using System;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;

public enum RgfPermissionType
{
    QueryString = 1001,
    QuickWatch = 1002,
    PublicFilterSetting = 1003,
    Export = 1004,
    PublicGridSetting = 1005,
    PublicChartSetting = 1006,

    FEPApprove = 1101,
    FEPReject = 1102,
    FEPRevise = 1103
}

/// <summary>
/// Type of Operation.
/// </summary>
/// <remarks>CRUD, VADER, CRAM</remarks>
[Flags]
public enum RgfOperationType
{
    //CRUD: Create, Read, Update and Delete
    /// <summary>
    /// Create.
    /// </summary>
    /// <remarks>Equivalent with Add.</remarks>
    Create = 1,
    /// <summary>
    /// Read.
    /// </summary>
    /// <remarks>Equivalent with View and Access.</remarks>
    Read = 2,
    /// <summary>
    /// Update.
    /// </summary>
    /// <remarks>Equivalent with Edit and Modify.</remarks>
    Update = 4,
    /// <summary>
    /// Delete.
    /// </summary>
    /// <remarks>Equivalent with Remove.</remarks>
    Delete = 8,
    /// <summary>
    /// Change of permission.
    /// </summary>
    /// <remarks>
    /// Reserve for future feature.
    /// </remarks>
    ChangePermission = 16,

    //VADE(R): view, add, delete, edit (and restore)
    /// <summary>
    /// View.
    /// </summary>
    /// <remarks>Equivalent with Read and Access.</remarks>
    View = 2,
    /// <summary>
    /// Add.
    /// </summary>
    /// <remarks>Equivalent with Create.</remarks>
    Add = 1,
    /// <summary>
    /// Edit.
    /// </summary>
    /// <remarks>Equivalent with Update and Modify.</remarks>
    Edit = 4,

    //CRAM: create, remove, access, modify
    /// <summary>
    /// Remove.
    /// </summary>
    /// <remarks>Equivalent with Delete.</remarks>
    Remove = 8,
    /// <summary>
    /// Access.
    /// </summary>
    /// <remarks>Equivalent with Read and View.</remarks>
    Access = 2,
    /// <summary>
    /// Modify.
    /// </summary>
    /// <remarks>Equivalent with Update and Edit.</remarks>
    Modify = 4
}

[Flags]
public enum RgfFEPType
{
    Invalid = 0,
    Add = 1,
    Edit = 2,
    Delete = 3,
    Approve = 4,
    Reject = 5,
    Revise = 6,
    ReviseA = 7,
    Update, //ez csak a kódban szerepel -> sajátját módosíthatja
    Validation //ez csak a kódban szerepel
}