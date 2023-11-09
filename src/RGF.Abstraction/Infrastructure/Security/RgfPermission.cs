using Recrovit.RecroGridFramework.Abstraction.Extensions;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace Recrovit.RecroGridFramework.Abstraction.Infrastructure.Security;

/// <summary>
/// Helper class for base permissions.
/// </summary>
public struct BasePermissions
{
    private ushort _permissions;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasePermissions"/> struct.
    /// </summary>
    /// <param name="administrator">if set to <c>true</c> then all base permissions set to allowed.</param>
    public BasePermissions(bool administrator = false)
    {
        this._permissions = (ushort)(administrator ? UInt16.MaxValue : 0);
    }

    /// <summary>
    /// Gets the specified permission.
    /// </summary>
    /// <param name="operationType">Type of the permission.</param>
    /// <returns><c>true</c> if permitted to type; otherwise, <c>false</c>.</returns>
    public bool Get(RgfOperationType operationType) { return (_permissions & (short)operationType) != 0; }
    /// <summary>
    /// Sets the specified permission.
    /// </summary>
    /// <param name="operationType">Type of the permission.</param>
    /// <param name="permission"><c>true</c> to allow; otherwise denied.</param>
    public void Set(RgfOperationType operationType, bool permission)
    {
        if (operationType.IsValid())
        {
            if (permission)
            {
                this._permissions |= (ushort)operationType;
            }
            else
            {
                this._permissions &= (ushort)~(ushort)operationType;
            }
        }
    }

    /// <summary>
    /// Gets or sets permission to Create.
    /// </summary>
    /// <value><c>true</c> if allowed to Create; otherwise, <c>false</c>.</value>
    public bool Create { get { return (_permissions & (short)RgfOperationType.Create) != 0; } set { Set(RgfOperationType.Create, value); } }
    /// <summary>
    /// Gets or sets permission to Read.
    /// </summary>
    /// <value><c>true</c> if allowed to Read; otherwise, <c>false</c>.</value>
    public bool Read { get { return (_permissions & (short)RgfOperationType.Read) != 0; } set { Set(RgfOperationType.Read, value); } }
    /// <summary>
    /// Gets or sets permission to Update.
    /// </summary>
    /// <value><c>true</c> if allowed to Update; otherwise, <c>false</c>.</value>
    public bool Update { get { return (_permissions & (short)RgfOperationType.Update) != 0; } set { Set(RgfOperationType.Update, value); } }
    /// <summary>
    /// Gets or sets permission to Delete.
    /// </summary>
    /// <value><c>true</c> if allowed to Delete; otherwise, <c>false</c>.</value>
    public bool Delete { get { return (_permissions & (short)RgfOperationType.Delete) != 0; } set { Set(RgfOperationType.Delete, value); } }
    /// <exclude />
    public bool ChangePermission { get { return (_permissions & (short)RgfOperationType.ChangePermission) != 0; } set { Set(RgfOperationType.ChangePermission, value); } }

    /// <summary>
    /// Gets or sets permission to View.
    /// </summary>
    /// <value><c>true</c> if allowed to View; otherwise, <c>false</c>.</value>
    public bool View { get { return Read; } set { Read = value; } }
    /// <summary>
    /// Gets or sets permission to Add.
    /// </summary>
    /// <value><c>true</c> if allowed to Add; otherwise, <c>false</c>.</value>
    public bool Add { get { return Create; } set { Create = value; } }
    /// <summary>
    /// Gets or sets permission to Edit.
    /// </summary>
    /// <value><c>true</c> if allowed to Edit; otherwise, <c>false</c>.</value>
    public bool Edit { get { return Update; } set { Update = value; } }

    /// <summary>
    /// Gets or sets permission to Remove.
    /// </summary>
    /// <value><c>true</c> if allowed to Remove; otherwise, <c>false</c>.</value>
    public bool Remove { get { return Delete; } set { Delete = value; } }
    /// <summary>
    /// Gets or sets permission to Access.
    /// </summary>
    /// <value><c>true</c> if allowed to Access; otherwise, <c>false</c>.</value>
    public bool Access { get { return Read; } set { Read = value; } }
    /// <summary>
    /// Gets or sets permission to Modify.
    /// </summary>
    /// <value><c>true</c> if allowed to Modify; otherwise, <c>false</c>.</value>
    public bool Modify { get { return Update; } set { Update = value; } }
}

[Serializable]
public class RgfPermissions
{
    public RgfPermissions() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="RgfPermissions"/> class.
    /// </summary>
    /// <param name="fullPermission">if <c>true</c> all base permissions are allowed.</param>
    public RgfPermissions(bool fullPermission = false) { this.IsFullPermission = fullPermission; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RgfPermissions"/> class.
    /// </summary>
    /// <param name="permissions">Permissions.</param>
    public RgfPermissions(RgfPermissions permissions)
    {
        foreach (var item in permissions)
        {
            this.Permissions.Add(item.Key, item.Value);
        }
        IsFullPermission = permissions.IsFullPermission;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RgfPermissions"/> class.
    /// </summary>
    /// <param name="CRUD">The CRUD.</param>
    public RgfPermissions(string CRUD) { this.CRUD = CRUD; }

    [JsonInclude]
    public Dictionary<int, bool> Permissions { get; protected set; } = new ();

    /// <summary>
    /// Gets the permission identifier.
    /// </summary>
    /// <param name="permissionName">Name of the permission.</param>
    /// <returns>The permission identifier.</returns>
    public virtual int GetPermissionId(string permissionName)
    {
        int id = 0;
        permissionName = permissionName.ToLower();
        switch (permissionName)
        {
            case "create":
            case "add":
                id = (int)RgfOperationType.Create;
                break;

            case "read":
            case "view":
            case "access":
                id = (int)RgfOperationType.Read;
                break;

            case "update":
            case "edit":
            case "modify":
                id = (int)RgfOperationType.Update;
                break;

            case "delete":
            case "remove":
                id = (int)RgfOperationType.Delete;
                break;

            case "changepermission":
                id = (int)RgfOperationType.ChangePermission;
                break;
        }
        return id;
    }

    /// <summary>
    /// Gets the base permissions.
    /// </summary>
    /// <value>
    /// The base permissions.
    /// </value>
    [JsonIgnore]
    public BasePermissions BasePermissions
    {
        get
        {
            BasePermissions bp;
            if (this.IsFullPermission)
            {
                bp = new BasePermissions(true);
            }
            else
            {
                bp = new BasePermissions(false);
                bp.Create = this.GetPermission(RgfOperationType.Create);
                bp.Read = this.GetPermission(RgfOperationType.Read);
                bp.Update = this.GetPermission(RgfOperationType.Update);
                bp.Delete = this.GetPermission(RgfOperationType.Delete);
                bp.ChangePermission = this.GetPermission(RgfOperationType.ChangePermission);
            }
            return bp;
        }
    }

    /// <summary>
    /// Gets or sets the acronym CRUD (Create, Read, Update, Delete). 
    /// </summary>
    /// <value>
    /// String of combination from CRUD.
    /// </value>
    [JsonIgnore]
    public string CRUD
    {
        get
        {
            StringBuilder crud = new StringBuilder();
            if (this.GetPermission(RgfOperationType.Create))
            {
                crud.Append("C");
            }
            if (this.GetPermission(RgfOperationType.Read))
            {
                crud.Append("R");
            }
            if (this.GetPermission(RgfOperationType.Update))
            {
                crud.Append("U");
            }
            if (this.GetPermission(RgfOperationType.Delete))
            {
                crud.Append("D");
            }
            return crud.ToString();
        }
        set
        {
            string crud = value ?? "";
            this.AddOrReplace(RgfOperationType.Create, crud.Contains("C"));
            this.AddOrReplace(RgfOperationType.Read, crud.Contains("R"));
            this.AddOrReplace(RgfOperationType.Update, crud.Contains("U"));
            this.AddOrReplace(RgfOperationType.Delete, crud.Contains("D"));
        }
    }

    /// <summary>
    /// Checks specified CRUD permissions simultaneously.
    /// </summary>
    /// <param name="sCRUD">String of combination from CRUD.</param>
    /// <returns>String of permissions from CRUD.</returns>
    public string GetPermissions(string sCRUD)
    {
        if (sCRUD == null)
        {
            return this.CRUD;
        }
        sCRUD = sCRUD.ToUpper();
        StringBuilder crud = new StringBuilder();
        if (sCRUD.Contains('C') && this.GetPermission(RgfOperationType.Create))
        {
            crud.Append("C");
        }
        if (sCRUD.Contains('R') && this.GetPermission(RgfOperationType.Read))
        {
            crud.Append("R");
        }
        if (sCRUD.Contains('U') && this.GetPermission(RgfOperationType.Update))
        {
            crud.Append("U");
        }
        if (sCRUD.Contains('D') && this.GetPermission(RgfOperationType.Delete))
        {
            crud.Append("D");
        }
        return crud.ToString();
    }

    /// <summary>
    /// Sets the maximum CRUD.
    /// </summary>
    /// <param name="maxCRUD">String of combination from CRUD.</param>
    /// <returns>The CRUD.</returns>
    public string SetMaxCRUD(string maxCRUD)
    {
        this.CRUD = this.GetPermissions(maxCRUD);
        return this.CRUD;
    }

    /// <summary>
    /// Gets a value indicating whether this instance includes all base permissions is allowed.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance includes all base permissions is allowed; otherwise, <c>false</c>.
    /// </value>
    [JsonInclude]
    public bool IsFullPermission { get; protected set; }

    /// <summary>
    /// Adds the specified permission type.
    /// </summary>
    /// <param name="type">The permission type.</param>
    /// <param name="permission">Set <c>true</c> to allow or <c>false</c> to deny.</param>
    public void Add(Enum type, bool permission) => this.Add(Convert.ToInt32(type), permission);

    /// <summary>
    /// Adds permission type of specified identifier.
    /// </summary>
    /// <param name="permissionId">The permission identifier.</param>
    /// <param name="permission">Set <c>true</c> to allow or <c>false</c> to deny.</param>
    public void Add(int permissionId, bool permission)
    {
        if (!this.IsFullPermission)
        {
            this.Permissions.Add(permissionId, permission);
        }
    }

    /// <summary>
    /// Adds permission type of specified name.
    /// </summary>
    /// <param name="permissionName">Name of the permission type.</param>
    /// <param name="permission">Set <c>true</c> to allow or <c>false</c> to deny.</param>
    public void Add(string permissionName, bool permission)
    {
        if (!this.IsFullPermission)
        {
            int id = GetPermissionId(permissionName);
            if (id != 0)
            {
                this.Permissions.Add(id, permission);
            }
        }
    }

    /// <summary>
    /// Adds or replaces permission type of specified name.
    /// </summary>
    /// <param name="type">The permission type.</param>
    /// <param name="permission">Set <c>true</c> to allow or <c>false</c> to deny.</param>
    public void AddOrReplace(Enum type, bool permission) => this.AddOrReplace(Convert.ToInt32(type), permission);

    /// <summary>
    /// Adds or replaces permission type of specified identifier.
    /// </summary>
    /// <param name="permissionId">The permission identifier.</param>
    /// <param name="permission">Set <c>true</c> to allow or <c>false</c> to deny.</param>
    public void AddOrReplace(int permissionId, bool permission)
    {
        if (!this.IsFullPermission)
        {
            bool perm;
            if (this.Permissions.TryGetValue(permissionId, out perm) == false)
            {
                this.Add(permissionId, permission);
            }
            else
            {
                this.Permissions[permissionId] = permission;
            }
        }
    }

    /// <summary>
    /// Adds or replaces permission type of specified name.
    /// </summary>
    /// <param name="permissionName">Name of the permission type.</param>
    /// <param name="permission">Set <c>true</c> to allow or <c>false</c> to deny.</param>
    public void AddOrReplace(string permissionName, bool permission)
    {
        if (!this.IsFullPermission)
        {
            int id = GetPermissionId(permissionName);
            if (id != 0)
            {
                AddOrReplace(id, permission);
            }
        }
    }

    /// <summary>
    /// Adds or replaces permissions.
    /// </summary>
    /// <param name="permissions">Permissions of User.</param>
    public void AddOrReplace(RgfPermissions permissions)
    {
        foreach (var item in permissions)
        {
            AddOrReplace(item.Key, item.Value);
        }
    }

    /// <summary>
    /// Gets the permission.
    /// </summary>
    /// <param name="type">The permission type. OperationType, RgfPermissionType</param>
    /// <returns><c>true</c> if permission type is allowed; otherwise <c>false</c>.</returns>
    public bool GetPermission(Enum type) => this.GetPermission(Convert.ToInt32(type));

    /// <summary>
    /// Gets the permission.
    /// </summary>
    /// <param name="permissionId">The permission identifier.</param>
    /// <returns><c>true</c> if permission type is allowed; otherwise <c>false</c>.</returns>
    public bool GetPermission(int permissionId)
    {
        if (this.IsFullPermission)
        {
            return true;
        }
        bool permission;
        if (this.Permissions.TryGetValue(permissionId, out permission) == false)
        {
            permission = false;
        }
        return permission;
    }

    /// <summary>
    /// Gets the permission.
    /// </summary>
    /// <param name="permissionName">Name of the permission type.</param>
    /// <returns><c>true</c> if permission type is allowed; otherwise <c>false</c>.</returns>
    public bool GetPermission(string permissionName)
    {
        bool permission = this.IsFullPermission;
        //TODO: GetPermission -> IsUserAdmin
#if NETFULL//GetPermission -> IsUserAdmin
            permission = RecroSec.IsUserAdmin;
#endif
        if (!permission)
        {
            int id = GetPermissionId(permissionName);
            if (id != 0)
            {
                permission = GetPermission(id);
            }
        }
        return permission;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>
    /// An enumerator that can be used to iterate through the collection.
    /// </returns>
    public IEnumerator<KeyValuePair<int, bool>> GetEnumerator() => this.Permissions.AsEnumerable().GetEnumerator();
}
