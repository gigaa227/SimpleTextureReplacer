• An asset replacement only requires 2 things: the asset file (either image or bundle) and a .meta file
• While the asset's name does not matter, the meta file must be named the same as the image. For an image called myImage.png, you will name the meta file myImage.png.meta
• The mod supports .png, .jpg and .jpeg images as well as assetbundles (assetbundles must have the extension .bundle)
• The mod will search all subdirectories of the Resource Packs folder so feel free to use as many folders as you need to organize stuff
• There are currently 2 possible types of meta data
  ◘ An asset. Example: 
=================================================
asset
SomeBundle/NameHere
SOME_ResourCE_NameHere
=================================================
  ◘ A gameobject. Example: 
=================================================
gameobject
SomeBundle/NameHere
SOME_ResourCE_NameHere
Name_ofAsset_ToBeReplaced
=================================================
• Meta files are plain text files. You can use something like notepad++ or the default windows notepad to edit them
• A meta file can contain multiple sets of meta data separated by an empty line:
=================================================
asset
SomeBundle/NameHere
SOME_ResourCE_NameHere

gameobject
SomeBundle/NameHere
SOME_ResourCE_NameHere
Name_ofAsset_ToBeReplaced
=================================================
• For an assetbundle's meta data, there should be an extra line at the start of each meta data that specifies the asset name in the bundle:
=================================================
MyCustomAssetName
gameobject
SomeBundle/NameHere
SOME_ResourCE_NameHere
Name_ofAsset_ToBeReplaced
=================================================
• The asset/gameobject line of the meta file is case insensitive (for legacy reasons, you can also use "texture" instead of "asset")
• The following argument lines are all case sensitive
• For "gameobject" data, the "name of asset to be replaced" can also be a special identifier:
  ◘ A field identifier. For a field identifier, you'll need at least a component's class name and the name of the field you want to edit. For example, to replace the value of the field "_TargetMesh" on the component "CustomComponent", you'd put "Field:CustomComponent,_TargetMesh". A field identifier can also be followed by the object name of the specific component you want to edit (in case there's multiple components of the same type). To specify an object name, just add another "," followed by the object name: "Field:CustomComponent,_TargetMesh,Part 01"
  ◘ A dragon skin mesh identifier. This is specifically for setting the mesh on a DragonSkin. It's simply "DragonSkinMesh:" followed by the age to replace the mesh of. For Titan: "DragonSkinMesh:Titan", for adult: "DragonSkinMesh:Adult" (Keep in mind, if the skin does not have a "teen" skin it will use the adult skin)
• Meta data can also have up to 2 extra lines on the end for the quality index then filter mode for a texture asset (these are ignored for other asset types, filter mode is only for textures loaded directly from an image file)
  ◘ Quality index can be a number 0 to 2 or the word "low", "mid" or "high" (case insensitive)
  ◘ If not specified, defaults to high quality
  ◘ If no texture is supplied for a specific texture quality, the mod will attempt to generate a lower res image from a higher quality texture otherwise it'll reuse a lower quality texture
  ◘ The main purpose of this option is if you want to force the one texture to be used for all quality levels or want to provide the lower resolution images yourself instead of using the automatic downscaling
  ◘ Filter mode can be a number 0 to 2 or the word "point", "bilinear" or "trilinear" (case insensitive)
  ◘ If not specified, defaults to bilinear
• If you want some help finding the bundle name and resource name; adding a file called "DEBUG" to the Resource Packs folder will make the mod log all the asset and gameobject requests. The DEBUG file is only checked for on game start, so you'll need to restart the game to enable/disable this extra logging