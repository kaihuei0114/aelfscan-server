using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElfScanServer.HttpApi.Dtos;
using AutoMapper.Internal.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Volo.Abp;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.Users;
using IdentityUser = Volo.Abp.Identity.IdentityUser;
using IdentityRole = Volo.Abp.Identity.IdentityRole;
namespace AElfScanServer.HttpApi.Service;

public interface IUserAppService
{
    // Task<IdentityUserDto> RegisterUserWithOrganization(RegisterUserWithOrganizationInput input);

    Task RegisterAppAuthentication(string appId, string deployKey);

    Task<IdentityUserDto> GetUserInfoAsync();

    Task ResetPasswordAsync(string userName, string newPassword);

    Task<string> GetClientDisplayNameAsync(string clientId);

    Task<UserResp> CreateUser(UserReq req);

    Task ResetAdminPwd();
}

// public interface IOrganizationAppService
// {
//     Task<OrganizationUnitDto> CreateOrganizationUnitAsync(string displayName, Guid? parentId = null);
//
//     Task DeleteOrganizationUnitAsync(Guid id);
//
//     Task AddUserToOrganizationUnitAsync(Guid userId, Guid organizationUnitId);
//
//     Task<List<OrganizationUnitDto>> GetAllOrganizationUnitsAsync();
//
//     Task<OrganizationUnitDto> GetOrganizationUnitAsync(Guid id);
//
//     Task<List<IdentityUserDto>> GetUsersInOrganizationUnitAsync(Guid organizationUnitId);
//
//     Task<List<OrganizationUnitDto>> GetOrganizationUnitsByUserIdAsync(Guid userId);
// }

[RemoteService(IsEnabled = false)]
[DisableAuditing]
public class UserAppService : IdentityUserAppService, IUserAppService
{
    private readonly IOrganizationUnitRepository _organizationUnitRepository;
    private readonly ILookupNormalizer _lookupNormalizer;
    private readonly IOpenIddictApplicationManager _applicationManager;
    // private readonly IOpenIddictScopeRepository _openIddictScopeRepository;
    // private readonly IOrganizationAppService _organizationAppService;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IIdentityRoleRepository _roleRepository;

    public UserAppService(
        IdentityUserManager userManager,
        IIdentityUserRepository userRepository,
        IIdentityRoleRepository roleRepository,
        ILookupNormalizer lookupNormalizer,
        IOptions<IdentityOptions> identityOptions,
        IOpenIddictApplicationManager applicationManager,
        // IOrganizationAppService organizationAppService,
        IOrganizationUnitRepository organizationUnitRepository,
        IPermissionChecker permissionChecker, IdentityUserManager identityUserManager)
        : base(userManager, userRepository, roleRepository, identityOptions, permissionChecker)
    {
        _organizationUnitRepository = organizationUnitRepository;
        _lookupNormalizer = lookupNormalizer;
        _applicationManager = applicationManager;
        // _organizationAppService = organizationAppService;
        _identityUserManager = identityUserManager;
        _roleRepository = roleRepository;
    }


    public async Task ResetAdminPwd()
    {
        // if (await _openIddictScopeRepository.FindByNameAsync("AElfScanServer") == null)
        // {
        //     await _scopeManager.CreateAsync(new OpenIddictScopeDescriptor {
        //         Name = "AElfScanServer", DisplayName = "AElfScanServer API", Resources = { "AElfScanServer" }
        //     });
        // }
        
        var adminUser = await _identityUserManager.FindByNameAsync("admin");
        if (adminUser != null)
        {
            var adminPassword = "Xuan123,.";
            var token = await _identityUserManager.GeneratePasswordResetTokenAsync(adminUser);
            var result = await _identityUserManager.ResetPasswordAsync(adminUser, token, adminPassword);
            if (!result.Succeeded)
            {
                throw new Exception("Failed to set admin password: " + result.Errors.Select(e => e.Description)
                    .Aggregate((errors, error) => errors + ", " + error));
            }
        }

        var normalizedRoleName = _lookupNormalizer.NormalizeName("appAdmin");
        var appAdminRole = await _roleRepository.FindByNormalizedNameAsync(normalizedRoleName);

        if (appAdminRole == null)
        {
            appAdminRole = new IdentityRole(Guid.NewGuid(), "appAdmin")
            {
                IsStatic = true,
                IsPublic = true
            };
            await _roleRepository.InsertAsync(appAdminRole);
        }
    }

    public async Task<UserResp> CreateUser(UserReq req)
    {
        var user = new IdentityUser(GuidGenerator.Create(), req.UserName, "test@qq.com");
        var createResult = await UserManager.CreateAsync(user, req.Password);
        if (!createResult.Succeeded)
        {
            throw new UserFriendlyException("Failed to create user. " + createResult.Errors.Select(e => e.Description)
                .Aggregate((errors, error) => errors + ", " + error));
        }
        

        var normalizedRoleName = _lookupNormalizer.NormalizeName("appAdmin");
        var identityUser = await UserManager.FindByIdAsync(user.Id.ToString());
        var appAdminRole = await RoleRepository.FindByNormalizedNameAsync(normalizedRoleName);

        if (appAdminRole != null)
        {
            await UserManager.AddToRoleAsync(identityUser, appAdminRole.Name);
        }

        return new UserResp()
        {
            UserName = req.UserName
        };
    }

    // public async Task<IdentityUserDto> RegisterUserWithOrganization(RegisterUserWithOrganizationInput input)
    // {
    //     var userName = input.UserName.Trim();
    //     var email = input.Email.Trim();
    //     var existUser = await UserManager.FindByNameAsync(userName);
    //     if (existUser != null && !existUser.Id.ToString().IsNullOrEmpty())
    //     {
    //         throw new UserFriendlyException($"user {existUser.Name} is already exist!");
    //     }
    //
    //     var user = new IdentityUser(GuidGenerator.Create(), userName, email, CurrentTenant.Id);
    //
    //     if (input.OrganizationUnitId.IsNullOrEmpty())
    //     {
    //         throw new UserFriendlyException("Failed to create user. OrganizationUnitId is null");
    //     }
    //
    //     Guid organizationUnitGuid;
    //     if (!Guid.TryParse(input.OrganizationUnitId, out organizationUnitGuid))
    //     {
    //         throw new UserFriendlyException("Invalid OrganizationUnitId string");
    //     }
    //
    //     OrganizationUnitDto organizationUnitDto =
    //         await _organizationAppService.GetOrganizationUnitAsync(organizationUnitGuid);
    //     if (organizationUnitDto == null || organizationUnitDto.Id.ToString().IsNullOrEmpty())
    //     {
    //         throw new UserFriendlyException($"OrganizationUnit {organizationUnitGuid} is not exist");
    //     }
    //
    //     var createResult = await UserManager.CreateAsync(user, input.Password);
    //     if (!createResult.Succeeded)
    //     {
    //         throw new UserFriendlyException("Failed to create user. " + createResult.Errors.Select(e => e.Description)
    //             .Aggregate((errors, error) => errors + ", " + error));
    //     }
    //
    //     //add appAdmin role into user
    //     var normalizedRoleName = _lookupNormalizer.NormalizeName("appAdmin");
    //     var identityUser = await UserManager.FindByIdAsync(user.Id.ToString());
    //     var appAdminRole = await RoleRepository.FindByNormalizedNameAsync(normalizedRoleName);
    //
    //     if (appAdminRole != null)
    //     {
    //         await UserManager.AddToRoleAsync(identityUser, appAdminRole.Name);
    //     }
    //
    //     // bind organization with user
    //     var organizationUnit = await _organizationUnitRepository.GetAsync(organizationUnitDto.DisplayName);
    //     await UserManager.AddToOrganizationUnitAsync(identityUser, organizationUnit);
    //     await _organizationAppService.AddUserToOrganizationUnitAsync(identityUser.Id, organizationUnit.Id);
    //
    //     return ObjectMapper.Map<IdentityUser, IdentityUserDto>(identityUser);
    // }

    public async Task RegisterAppAuthentication(string appId, string deployKey)
    {
        if (await _applicationManager.FindByClientIdAsync(appId) != null)
        {
            throw new UserFriendlyException("A app with the same ID already exists.");
        }

        await _applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = appId,
            ClientSecret = deployKey,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = " Apps",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "AeFinder",
                OpenIddictConstants.Permissions.ResponseTypes.IdToken
            }
        });
    }

    public async Task<IdentityUserDto> GetUserInfoAsync()
    {
        if (CurrentUser == null || CurrentUser.Id == null)
        {
            throw new UserFriendlyException("CurrentUser is null");
        }

        var identityUser = await UserManager.FindByIdAsync(CurrentUser.Id.ToString());
        if (identityUser == null)
        {
            throw new UserFriendlyException("user not found.");
        }

        return ObjectMapper.Map<IdentityUser, IdentityUserDto>(identityUser);
    }

    public async Task<string> GetClientDisplayNameAsync(string clientId)
    {
        var openIddictApplication = await _applicationManager.FindByClientIdAsync(clientId);
        // return openIddictApplication.DisplayName;

        var displayName = (string)openIddictApplication.GetType().GetProperty("DisplayName")
            ?.GetValue(openIddictApplication);

        if (!string.IsNullOrEmpty(displayName))
        {
            return displayName;
        }

        return string.Empty;
    }

    public async Task ResetPasswordAsync(string userName, string newPassword)
    {
        if (CurrentUser == null || CurrentUser.Id == null)
        {
            throw new UserFriendlyException("CurrentUser is null");
        }

        if (CurrentUser.UserName != userName)
        {
            throw new UserFriendlyException("Can only reset your own password");
        }

        var identityUser = await UserManager.FindByIdAsync(CurrentUser.Id.ToString());
        if (identityUser == null)
        {
            throw new UserFriendlyException("user not found.");
        }

        var token = await UserManager.GeneratePasswordResetTokenAsync(identityUser);
        var result = await UserManager.ResetPasswordAsync(identityUser, token, newPassword);
        if (!result.Succeeded)
        {
            throw new UserFriendlyException("reset user password failed." + result.Errors.Select(e => e.Description)
                .Aggregate((errors, error) => errors + ", " + error));
        }
    }
}