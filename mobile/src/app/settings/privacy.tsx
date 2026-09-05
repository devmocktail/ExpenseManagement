import MaterialCommunityIcons from '@expo/vector-icons/MaterialCommunityIcons';
import { useRouter } from 'expo-router';
import { ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { AppText } from '@/components/ui/AppText';
import { ScreenHeader } from '@/components/ui/ScreenHeader';
import { useAppTheme } from '@/theme/ThemeProvider';

/**
 * Privacy policy — placeholder copy.
 *
 * Everything below describes how the app actually behaves today, in plain
 * language, with square brackets wherever a real legal document needs a fact
 * only the publisher can supply. It is deliberately free of jurisdictions,
 * company names and statutory claims: inventing those would be worse than
 * leaving them blank, because they read as reviewed when they are not.
 */
export default function PrivacyPolicyScreen() {
  const theme = useAppTheme();
  const router = useRouter();
  const insets = useSafeAreaInsets();

  return (
    <View style={[styles.flex, { backgroundColor: theme.c.background }]}>
      <ScreenHeader title="Privacy policy" onBack={() => router.back()} />

      <ScrollView
        contentContainerStyle={{
          padding: theme.spacing.base,
          paddingBottom: insets.bottom + theme.spacing.xxl,
          gap: theme.spacing.lg,
        }}
        showsVerticalScrollIndicator={false}
      >
        <TemplateBanner />

        <View>
          <AppText variant="heading2" accessibilityRole="header">
            Privacy policy
          </AppText>
          <AppText variant="caption" color="textTertiary" style={{ marginTop: theme.spacing.xs }}>
            Last updated: [date] · Applies to the [app name] mobile app
          </AppText>
        </View>

        <Section title="The short version">
          <Paragraph>
            This app stores the money records you enter so it can show them back to you. It is not
            an advertising product: your transactions are not sold, and they are not shared with
            anyone except the service providers listed below that are needed to run the app.
          </Paragraph>
        </Section>

        <Section title="What we collect">
          <Paragraph>
            Account details you give us: your name, your email address and a password, which is
            stored only as a one-way hash and is never readable by us.
          </Paragraph>
          <Paragraph>
            Financial records you enter: amounts, dates, categories, payment methods, merchants,
            notes and any receipt images you attach. You choose what goes in; the app never reads
            your bank accounts or messages.
          </Paragraph>
          <Paragraph>
            Technical data needed to operate the service: device and app version, a push
            notification token if you enable reminders, and server logs containing IP address and
            request timestamps.
          </Paragraph>
        </Section>

        <Section title="What we do with it">
          <Paragraph>
            We use your data to run the features you asked for — showing your dashboard, computing
            budgets and analytics, sending the notifications you switched on — and to keep the
            service secure and working, including diagnosing faults from logs.
          </Paragraph>
          <Paragraph>
            [Set out the legal basis for each purpose in the reviewed version, for the regimes this
            app is published under.]
          </Paragraph>
        </Section>

        <Section title="Who else sees it">
          <Paragraph>
            [List every processor and sub-processor here: hosting, database, object storage for
            receipts, push notification delivery, crash reporting and analytics, with the country
            each operates in.] Each one is contractually limited to processing data on our
            instructions.
          </Paragraph>
          <Paragraph>
            We disclose data outside that list only where we are legally compelled to, and we will
            tell you when we are permitted to.
          </Paragraph>
        </Section>

        <Section title="How long we keep it">
          <Paragraph>
            Your records are kept while your account exists. Deleting your account removes them from
            the live system straight away; encrypted backups age out on their own retention
            schedule, so a copy may persist there for a short period before being overwritten.
            [State the exact backup retention window.]
          </Paragraph>
        </Section>

        <Section title="Your choices">
          <Paragraph>
            You can edit or delete any transaction at any time, export everything you have entered
            from Settings, and delete your account and its data from Settings. Notification
            categories can be turned off individually.
          </Paragraph>
          <Paragraph>
            [Describe the statutory rights that apply — access, correction, portability, erasure,
            objection — and how to exercise them, once the applicable regimes are confirmed.]
          </Paragraph>
        </Section>

        <Section title="Security">
          <Paragraph>
            Traffic between the app and the server is encrypted in transit. Sign-in tokens are held
            in the device keystore rather than ordinary app storage, and passwords are stored only
            as salted hashes. No system is perfect, and we will notify affected users of a breach as
            required. [Add the breach notification commitment and timeline.]
          </Paragraph>
        </Section>

        <Section title="Children">
          <Paragraph>
            The app is not directed at children. [State the minimum age for the markets this app is
            published in, and what happens if an underage account is discovered.]
          </Paragraph>
        </Section>

        <Section title="Changes and contact">
          <Paragraph>
            If this policy changes in a way that affects you, we will tell you in the app before the
            change takes effect. Questions or requests go to [contact email], operated by [company
            name and registered address].
          </Paragraph>
        </Section>
      </ScrollView>
    </View>
  );
}

/** Shown at the top of both policy screens so a draft can never ship unnoticed. */
function TemplateBanner() {
  const theme = useAppTheme();

  return (
    <View
      accessibilityRole="alert"
      style={[
        styles.banner,
        {
          gap: theme.spacing.sm,
          backgroundColor: theme.c.warningMuted,
          borderRadius: theme.radius.medium,
          padding: theme.spacing.md,
        },
      ]}
    >
      <MaterialCommunityIcons name="alert-outline" size={18} color={theme.c.warning} />
      <View style={styles.flex}>
        <AppText variant="bodySmallStrong" color="warning">
          Template — not legal advice
        </AppText>
        <AppText variant="caption" color="textSecondary" style={{ marginTop: 2 }}>
          Placeholder copy for layout and review. It must be replaced with text approved by a
          qualified adviser, and every [bracketed] item filled in, before release.
        </AppText>
      </View>
    </View>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  const theme = useAppTheme();

  return (
    <View style={{ gap: theme.spacing.sm }}>
      <AppText variant="heading3" accessibilityRole="header">
        {title}
      </AppText>
      {children}
    </View>
  );
}

function Paragraph({ children }: { children: React.ReactNode }) {
  return (
    <AppText variant="bodySmall" color="textSecondary">
      {children}
    </AppText>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  banner: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
});
