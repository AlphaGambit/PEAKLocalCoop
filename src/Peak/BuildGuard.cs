using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace PeakLocalCoop;

internal static class BuildGuard
{
    private static readonly (string Path, string Hash)[] Files =
    {
        ("Managed/Assembly-CSharp.dll", "a3643f11dc444d86491270b224c0a0caf9126a10b26731feb97fc85ad1d82038"),
        ("Managed/PhotonUnityNetworking.dll", "d0aa090013a5327d35e642dcdcc4797857b81a994d50eba3394aeb24fa5cd11f"),
        ("Managed/PhotonRealtime.dll", "857240a5403ebae9319abc8d4617f140cbc85e744109e386f65664a4796fb3a4"),
        ("Managed/Zorro.ControllerSupport.dll", "d5ac307465994a46ee74c5e4313cf9470382c12dc4c600ff1980c7e84f0c8487"),
        ("Managed/Unity.RenderPipelines.Universal.Runtime.dll", "66c6a881c13cd4a2a35bf25d2fddfd136db725e7f4a0b9210b2bcbb9870234cb"),
        ("Managed/Unity.InputSystem.dll", "acfe2788d3f891022178c0ee1bf4d307473a99079925b3cbb2418dde8e615850"),
        ("globalgamemanagers", "81806cade6c480a9ba4beb0b13c686d3785fe3e6a5d422d4f7da0e472455a5e1"),
    };

    public static void Verify()
    {
        if (Application.unityVersion != "6000.3.15f1")
            throw new InvalidOperationException($"Unity {Application.unityVersion} belum diaudit.");
        using var sha = SHA256.Create();
        foreach (var file in Files)
        {
            using var stream = File.OpenRead(Path.Combine(Application.dataPath, file.Path));
            var digest = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            if (!string.Equals(digest, file.Hash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Build PEAK belum diaudit. SHA-256 {file.Path} = {digest}.");
        }
    }

    public static MethodInfo Method(Type type, string name, params Type[] args) =>
        AccessTools.DeclaredMethod(type, name, args)
        ?? throw new MissingMethodException(type.FullName, name);

    public static MethodInfo MethodByName(Type type, string name) =>
        AccessTools.DeclaredMethod(type, name) ?? throw new MissingMethodException(type.FullName, name);

    public static FieldInfo Field(Type type, string name) =>
        AccessTools.DeclaredField(type, name) ?? throw new MissingFieldException(type.FullName, name);
}
