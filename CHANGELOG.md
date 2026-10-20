# Changelog

## 0.1.0 (2026-10-20)


### Features

* **addin:** add Outlook COM add-in shell with ribbon gallery ([8617c4e](https://github.com/abinnovision/outlook-quick-attach/commit/8617c4e40068f55b07e288134e323af2811ff931))
* **addin:** list files in a dynamic menu like Recent Items ([ce4d5a1](https://github.com/abinnovision/outlook-quick-attach/commit/ce4d5a1fd3ae0fda224a3f60c49362de21252eea))
* **addin:** show built-in icons for the folder and settings actions ([#7](https://github.com/abinnovision/outlook-quick-attach/issues/7)) ([4008f89](https://github.com/abinnovision/outlook-quick-attach/commit/4008f8998294bcdfec2ee3d4d1e0e5cb8e50f2b1))
* **addin:** use English for built-in labels and UI strings ([d89f53a](https://github.com/abinnovision/outlook-quick-attach/commit/d89f53a47f41e9ffa7a720310396f3b2984c6ae0))
* **core:** add layered configuration resolver and central config ([1472154](https://github.com/abinnovision/outlook-quick-attach/commit/14721547bcac64a81a6310faa6f9aa78c904c4f1))
* **core:** add scan repository with timeout and cache ([7597f1a](https://github.com/abinnovision/outlook-quick-attach/commit/7597f1ad2a92b6c70f2a6230f084cfeb33f3ce58))
* **core:** add shared log file and msi version reader ([d07aa33](https://github.com/abinnovision/outlook-quick-attach/commit/d07aa33a5b3d436313780d99fd0be6b0da60d01e))
* **core:** add version policy and gallery label formatter ([0f37ead](https://github.com/abinnovision/outlook-quick-attach/commit/0f37eadf3ba8f31418f999c4e3533a701a35be53))
* **deploy:** add config template and idempotent install script ([a418f7b](https://github.com/abinnovision/outlook-quick-attach/commit/a418f7be2455bd2144cf8417b96e96ffe98952e3))
* **installer:** add per-user WiX MSI ([b1d4d3f](https://github.com/abinnovision/outlook-quick-attach/commit/b1d4d3f3f1b13bd092f1edb43eb6fade642df305))
* **updater:** add logon self-updater ([c639e35](https://github.com/abinnovision/outlook-quick-attach/commit/c639e35682c916854fae06c86648c5442ce6288a))
* **updater:** verify the MSI signature before updating ([#6](https://github.com/abinnovision/outlook-quick-attach/issues/6)) ([0ab64e8](https://github.com/abinnovision/outlook-quick-attach/commit/0ab64e875a271392c3c13d8b02f42b8e8fe424bd))


### Bug Fixes

* **addin:** avoid blocking the UI thread on central config reads ([abeb87b](https://github.com/abinnovision/outlook-quick-attach/commit/abeb87b9f7fb123cfb313b8e726725ebf637a9cb))
* **addin:** keep Outlook responsive offline and surface errors ([#8](https://github.com/abinnovision/outlook-quick-attach/issues/8)) ([f29aa6d](https://github.com/abinnovision/outlook-quick-attach/commit/f29aa6dafcfd7f69dee63da0c6687f618731a34a))
* **addin:** marshal IDTExtensibility2 custom arrays as SAFEARRAY ([7f7bd88](https://github.com/abinnovision/outlook-quick-attach/commit/7f7bd880638f8d99a74abb96fab416c4f437fd38))
* **addin:** watch existing parent registry keys and start the watcher on the UI thread ([e864cb4](https://github.com/abinnovision/outlook-quick-attach/commit/e864cb4840b4d8f86c6a022297b806f15031844b))
* harden the add-in and updater and warn about invalid config ([#11](https://github.com/abinnovision/outlook-quick-attach/issues/11)) ([8c1e748](https://github.com/abinnovision/outlook-quick-attach/commit/8c1e7488d50abe6b8822e190e9722e10d369b0bc))
* **installer:** remember install fallback properties across upgrades ([0135c8e](https://github.com/abinnovision/outlook-quick-attach/commit/0135c8e53a0916a15318e9c6aea27660c3f20cb0))
* **updater:** start msiexec without waiting and suppress restarts ([9333216](https://github.com/abinnovision/outlook-quick-attach/commit/93332164bbd72f862afbc864615a15056bcc765c))
