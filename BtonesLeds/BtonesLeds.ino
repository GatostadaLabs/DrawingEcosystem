
#include <math.h>
#include <string.h>

const byte PIN_BOTON = 4;
const byte LED_PULSADOR = 5;
const byte TIRA_LED = 6;

// Brillos configurables (0-255)
const byte BRILLO_MINIMO = 120;
const byte BRILLO_PULSADOR = 255;
const byte BRILLO_TIRA = 255;

const unsigned long DURACION_RESPIRACION = 1500;
const byte NUM_RESPIRACIONES = 5;

const unsigned long DURACION_TOTAL =
    DURACION_RESPIRACION * NUM_RESPIRACIONES;

const unsigned long INICIO_APAGADO_TIRA =
    DURACION_TOTAL - DURACION_RESPIRACION;

const unsigned long ANTIRREBOTE = 30;

bool lecturaAnterior = HIGH;
bool estadoBoton = HIGH;
bool animacionActiva = false;

unsigned long ultimoCambioBoton = 0;
unsigned long inicioAnimacion = 0;

// Almacena los mensajes recibidos desde Unity
char mensajeSerial[32];
byte posicionSerial = 0;

void setup() {
  Serial.begin(9600);

  pinMode(PIN_BOTON, INPUT_PULLUP);
  pinMode(LED_PULSADOR, OUTPUT);
  pinMode(TIRA_LED, OUTPUT);

  establecerBrilloReposo();

  Serial.println("ARDUINO_READY");
}

void loop() {
  leerBoton();
  leerPuertoSerie();
  actualizarAnimacion();
}

// El botón solamente avisa a Unity
void leerBoton() {
  bool lecturaActual = digitalRead(PIN_BOTON);
  unsigned long ahora = millis();

  if (lecturaActual != lecturaAnterior) {
    lecturaAnterior = lecturaActual;
    ultimoCambioBoton = ahora;
  }

  if (ahora - ultimoCambioBoton >= ANTIRREBOTE) {
    if (lecturaActual != estadoBoton) {
      estadoBoton = lecturaActual;

      if (estadoBoton == LOW) {
        Serial.println("BUTTON_PRESSED");
      }
    }
  }
}

// Recibe mensajes completos terminados en salto de línea
void leerPuertoSerie() {
  while (Serial.available() > 0) {
    char caracter = Serial.read();

    if (caracter == '\n') {
      mensajeSerial[posicionSerial] = '\0';

      if (posicionSerial > 0) {
        procesarMensaje(mensajeSerial);
      }

      posicionSerial = 0;

    } else if (caracter != '\r') {
      if (posicionSerial < sizeof(mensajeSerial) - 1) {
        mensajeSerial[posicionSerial] = caracter;
        posicionSerial++;
      } else {
        posicionSerial = 0;
      }
    }
  }
}

void procesarMensaje(const char* mensaje) {
  if (strcmp(mensaje, "START_LIGHTS") == 0) {
    if (!animacionActiva) {
      iniciarAnimacion();
    } else {
      Serial.println("LIGHTS_BUSY");
    }
  } else {
    Serial.println("UNKNOWN_COMMAND");
  }
}

void iniciarAnimacion() {
  animacionActiva = true;
  inicioAnimacion = millis();

  Serial.println("LIGHTS_STARTED");
}

void actualizarAnimacion() {
  if (!animacionActiva) {
    return;
  }

  unsigned long tiempo =
      millis() - inicioAnimacion;

  if (tiempo >= DURACION_TOTAL) {
    animacionActiva = false;
    establecerBrilloReposo();

    Serial.println("LIGHTS_FINISHED");
    return;
  }

  actualizarLedPulsador(tiempo);
  actualizarTiraLed(tiempo);
}

// D5: cinco respiraciones entre brillo mínimo y máximo
void actualizarLedPulsador(unsigned long tiempo) {
  unsigned long fase =
      tiempo % DURACION_RESPIRACION;

  float progreso =
      (float)fase / DURACION_RESPIRACION;

  float respiracion =
      (1.0 - cos(2.0 * PI * progreso)) / 2.0;

  int brillo =
      BRILLO_MINIMO +
      (BRILLO_PULSADOR - BRILLO_MINIMO) * respiracion;

  analogWrite(LED_PULSADOR, brillo);
}

// D6: encendido suave, brillo fijo y apagado suave
// Nunca baja del brillo mínimo
void actualizarTiraLed(unsigned long tiempo) {
  int brillo;

  if (tiempo < DURACION_RESPIRACION) {
    float progreso =
        (float)tiempo / DURACION_RESPIRACION;

    float curva =
        (1.0 - cos(PI * progreso)) / 2.0;

    brillo =
        BRILLO_MINIMO +
        (BRILLO_TIRA - BRILLO_MINIMO) * curva;

  } else if (tiempo < INICIO_APAGADO_TIRA) {
    brillo = BRILLO_TIRA;

  } else {
    unsigned long tiempoApagado =
        tiempo - INICIO_APAGADO_TIRA;

    float progreso =
        (float)tiempoApagado /
        DURACION_RESPIRACION;

    float curva =
        (1.0 + cos(PI * progreso)) / 2.0;

    brillo =
        BRILLO_MINIMO +
        (BRILLO_TIRA - BRILLO_MINIMO) * curva;
  }

  analogWrite(TIRA_LED, brillo);
}

// Brillo permanente cuando no hay animación
void establecerBrilloReposo() {
  analogWrite(LED_PULSADOR, BRILLO_MINIMO);
  analogWrite(TIRA_LED, BRILLO_MINIMO);
}
