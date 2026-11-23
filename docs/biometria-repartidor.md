# Verificación del dispositivo para cambiar la contraseña

Reentrega, punto 4. La operación sensible elegida es **cambiar la contraseña del repartidor**.
Desde la hoja de ruta, el repartidor autenticado accede a **Seguridad de mi cuenta**. Para registrar
su primera passkey confirma la contraseña actual y usa el autenticador del dispositivo.
Para cambiar la contraseña se exige una nueva confirmación WebAuthn y la contraseña actual.

El servidor emite un desafío que dura dos minutos y se consume una vez. Está asociado al
operador, usuario y operación. ASP.NET Core Identity valida el origen, el desafío, la firma
y la verificación del usuario. El cambio y la actualización de la credencial se guardan juntos.
PostgreSQL guarda la credencial pública del usuario en `AspNetUserPasskeys`. El sistema operativo
maneja la huella o el rostro y determina si usa su código de desbloqueo como alternativa.

El alcance actual admite una passkey por cuenta para que una sesión con contraseña no pueda
agregar otra tras el primer registro. Si se pierde el acceso a esa passkey, este flujo no ofrece
recuperación autoservicio. Los desafíos viven en memoria del servidor y un reinicio obliga a
iniciar una verificación nueva. El diseño usa el despliegue actual de una sola instancia Web.

## Preparación y ensayo

1. Aplicá la migración `PasskeysRepartidor` y publicá la Web y la PWA en el mismo dominio HTTPS.
   En la PC de desarrollo usá `localhost` como nombre de host para la verificación WebAuthn.
2. En un celular con huella o rostro configurado, iniciá sesión con un repartidor de prueba.
3. Abrí **Seguridad de mi cuenta**, indicá la contraseña actual, tocá **Configurar passkey** y luego
   **Confirmar con el dispositivo**. Completá el diálogo del sistema.
4. Ingresá la contraseña actual y una nueva. Tocá **Preparar cambio de contraseña** y
   **Confirmar con el dispositivo**. Si cancelás el aviso, comprobá que la contraseña sigue igual.
5. Repetí el cambio y completá la huella o el rostro. Mostrá el diálogo del sistema y luego
   la confirmación. Volvé a iniciar sesión con la nueva contraseña.

El cambio de contraseña requiere conexión. La frase local del punto 9 se utiliza para
desbloquear datos offline y mantiene su propio flujo. La prueba E2E `BiometriaTests` usa un
autenticador virtual de Chromium para verificar registro, rechazo de confirmaciones inválidas o
repetidas y el inicio de sesión después del cambio. Para la evidencia biométrica se usa el sensor
del celular de la demostración.
